const express = require('express')
const cors = require('cors')
const multer = require('multer')
const path = require('path')
const fs = require('fs')
const fsp = require('fs/promises')
const os = require('os')
const { spawn } = require('child_process')

const app = express()

app.use(
  cors({
    origin: true,
    credentials: false,
  })
)

const uploadRoot = path.join(__dirname, 'uploads')
if (!fs.existsSync(uploadRoot)) fs.mkdirSync(uploadRoot, { recursive: true })

const upload = multer({
  dest: uploadRoot,
  limits: {
    fileSize: 300 * 1024 * 1024,
  },
})

let ffmpegAvailable = true
try {
  const p = spawn('ffmpeg', ['-version'], { windowsHide: true })
  p.on('error', (err) => {
    if (err && err.code === 'ENOENT') ffmpegAvailable = false
  })
  p.on('close', () => {})
} catch {
  ffmpegAvailable = false
}

function safeFormat(format) {
  const f = (format || 'srt').toLowerCase().trim()
  if (f === 'srt' || f === 'vtt' || f === 'json') return f
  return 'srt'
}

function safeModel(model) {
  const m = (model || 'small').toLowerCase().trim()
  if (['tiny', 'base', 'small', 'medium', 'large'].includes(m)) return m
  return 'small'
}

function safeLanguage(lang) {
  const l = (lang || 'vi').toLowerCase().trim()
  return l || 'vi'
}

function safeBool(v) {
  if (v === true) return true
  const s = String(v ?? '').toLowerCase().trim()
  return s === '1' || s === 'true' || s === 'yes' || s === 'on'
}

function runFfmpeg(args) {
  return new Promise((resolve, reject) => {
    const p = spawn('ffmpeg', args, { windowsHide: true })
    let stdout = ''
    let stderr = ''
    p.stdout.on('data', (d) => (stdout += d.toString()))
    p.stderr.on('data', (d) => (stderr += d.toString()))
    p.on('error', (err) => {
      reject(new Error(`Failed to start ffmpeg. Details: ${err.message}`))
    })
    p.on('close', (code) => {
      if (code !== 0) {
        reject(new Error(`ffmpeg failed (exit ${code}).\nSTDOUT:\n${stdout}\nSTDERR:\n${stderr}`))
        return
      }
      resolve({ stdout, stderr })
    })
  })
}

function runFfprobeDurationSeconds(inputPath) {
  return new Promise((resolve) => {
    const args = ['-v', 'error', '-show_entries', 'format=duration', '-of', 'default=noprint_wrappers=1:nokey=1', inputPath]
    const p = spawn('ffprobe', args, { windowsHide: true })
    let out = ''
    p.stdout.on('data', (d) => (out += d.toString()))
    p.on('error', () => resolve(null))
    p.on('close', (code) => {
      if (code !== 0) return resolve(null)
      const v = parseFloat(String(out).trim())
      if (!Number.isFinite(v)) return resolve(null)
      resolve(v)
    })
  })
}

function runWhisper({ inputPath, outDir, format, model, language, wordTimestamps }) {
  return new Promise((resolve, reject) => {
    const args = [
      inputPath,
      '--model',
      model,
      '--language',
      language,
      '-f',
      format,
      '-o',
      outDir,
      '--verbose',
      'False',
      '--temperature',
      '0',
      '--condition_on_previous_text',
      'False',
      '--fp16',
      'False',
      '--no_speech_threshold',
      '0.3',
      '--task',
      'transcribe',
    ]

    if (wordTimestamps) {
      args.push('--word_timestamps')
      args.push('True')
    }

    const pythonCmd = (process.env.PYTHON_CMD || 'python').trim()
    const pythonArgs = ['-m', 'whisper', ...args]

    const p = spawn(pythonCmd, pythonArgs, {
      windowsHide: true,
      env: {
        ...process.env,
        PYTHONIOENCODING: process.env.PYTHONIOENCODING || 'utf-8',
        PYTHONUTF8: process.env.PYTHONUTF8 || '1',
      },
    })

    let stdout = ''
    let stderr = ''

    p.stdout.on('data', (d) => (stdout += d.toString()))
    p.stderr.on('data', (d) => (stderr += d.toString()))

    p.on('error', (err) => {
      if (err && err.code === 'ENOENT') {
        reject(new Error('Python was not found in PATH. Set PYTHON_CMD env var to your python.exe (or install Python and add to PATH).'))
        return
      }
      reject(new Error(`Failed to start python whisper. Details: ${err.message}`))
    })

    p.on('close', (code) => {
      if (code !== 0) {
        const extra = !ffmpegAvailable
          ? '\nFFmpeg was not found in PATH. Install FFmpeg and add it to PATH, then restart the Node service.'
          : ''
        reject(new Error(`Whisper failed (exit ${code}).\nSTDOUT:\n${stdout}\nSTDERR:\n${stderr}${extra}`))
        return
      }
      resolve({ stdout, stderr })
    })
  })
}

app.post('/api/transcribe', upload.single('file'), async (req, res) => {
  const file = req.file
  if (!file) return res.status(400).json({ error: 'Missing form-data file field: file' })

  const format = safeFormat(req.body.format)
  const model = safeModel(req.body.model)
  const language = safeLanguage(req.body.language)
  const wordTimestamps = safeBool(req.body.word_timestamps)

  const reqId = `${Date.now()}_${Math.random().toString(16).slice(2)}`
  const workDir = path.join(os.tmpdir(), `whisper_${reqId}`)
  await fsp.mkdir(workDir, { recursive: true })

  const originalBase = path.parse(file.originalname || 'input').name || 'subtitle'
  const outputPath = path.join(workDir, `${originalBase}.${format}`)

  const findOutputFile = async () => {
    const targetExt = `.${format}`

    const scanDir = async (dir) => {
      const entries = await fsp.readdir(dir, { withFileTypes: true })
      for (const ent of entries) {
        const full = path.join(dir, ent.name)
        if (ent.isDirectory()) {
          const found = await scanDir(full)
          if (found) return found
          continue
        }
        if (ent.isFile() && ent.name.toLowerCase().endsWith(targetExt)) return full
      }
      return null
    }

    if (fs.existsSync(outputPath)) return outputPath
    return await scanDir(workDir)
  }

  try {
    // Normalize input audio for more stable timestamps/accuracy (especially for MP4)
    const wavPath = path.join(workDir, 'input.wav')
    try {
      await runFfmpeg([
        '-y',
        '-i',
        file.path,
        '-vn',
        '-ac',
        '1',
        '-ar',
        '16000',
        '-c:a',
        'pcm_s16le',
        '-af',
        'loudnorm=I=-16:TP=-1.5:LRA=11',
        wavPath,
      ])
    } catch (e) {
      // If ffmpeg fails for any reason, fallback to original file
      console.error('ffmpeg normalize failed, falling back to original input:', e?.message || e)
    }

    if (fs.existsSync(wavPath)) {
      const dur = await runFfprobeDurationSeconds(wavPath)
      if (dur !== null && dur < 0.5) {
        return res.status(400).json({
          error: 'Extracted audio is too short/empty. The input may have no audio track or is not decodable.',
          durationSeconds: dur,
        })
      }
    }

    const inputForWhisper = fs.existsSync(wavPath) ? wavPath : file.path

    const whisperRun = await runWhisper({
      inputPath: inputForWhisper,
      outDir: workDir,
      format,
      model,
      language,
      wordTimestamps,
    })

    const finalPath = await findOutputFile()
    if (!finalPath) {
      let entries = []
      try {
        entries = (await fsp.readdir(workDir)).slice(0, 50)
      } catch {}

      let inputExists = false
      let inputSize = 0
      try {
        const st = await fsp.stat(file.path)
        inputExists = st.isFile()
        inputSize = st.size
      } catch {}

      return res.status(500).json({
        error: `Output .${format} not found`,
        workDir,
        workDirEntries: entries,
        ffmpegAvailable,
        input: {
          path: file.path,
          originalname: file.originalname,
          mimetype: file.mimetype,
          size: file.size,
          inputExists,
          inputSize,
        },
        whisper: {
          stdout: whisperRun?.stdout ?? '',
          stderr: whisperRun?.stderr ?? '',
        },
      })
    }

    const downloadName = `subtitle.${format}`
    res.download(finalPath, downloadName, async (err) => {
      try {
        await fsp.rm(file.path, { force: true })
      } catch {}
      try {
        await fsp.rm(workDir, { recursive: true, force: true })
      } catch {}

      if (err) console.error('Download error:', err)
    })
  } catch (e) {
    try {
      await fsp.rm(file.path, { force: true })
    } catch {}
    try {
      await fsp.rm(workDir, { recursive: true, force: true })
    } catch {}

    console.error(e)
    res.status(500).json({
      error: 'Transcription failed',
      details: e?.message || String(e),
    })
  }
})

app.get('/health', (req, res) => res.json({ ok: true }))

const port = process.env.PORT || 3099
app.listen(port, () => {
  console.log(`Whisper Local Service running: http://localhost:${port}`)
})
