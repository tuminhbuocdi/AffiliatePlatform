<template>


    <div class="page-layout">
      <div class="page-main">
        <div class="card">
          <div class="row">
            <label class="field">
              <div class="lbl">File video/audio</div>
              <input class="input" type="file" accept="audio/*,video/*" :disabled="loading" @change="onPickFile" />
            </label>

            <label class="field">
              <div class="lbl">Ngôn ngữ (optional)</div>
              <input v-model="language" class="input" placeholder="vi / en ..." :disabled="loading" />
            </label>

            <label class="field">
              <div class="lbl">Output</div>
              <select v-model="outputFormat" class="input" :disabled="loading">
                <option value="srt">SRT</option>
                <option value="vtt">VTT</option>
                <option value="json">JSON</option>
              </select>
            </label>
          </div>

          <div class="actions">
            <button class="btn" type="button" :disabled="loading || !file" @click="generate">
              {{ loading ? 'Đang xử lý...' : 'Generate subtitle' }}
            </button>
            <button class="btn secondary" type="button" :disabled="loading || !resultBlob" @click="download">
              Download
            </button>
            <button class="btn secondary" type="button" :disabled="loading" @click="reset">
              Reset
            </button>
          </div>

          <div v-if="error" class="error">{{ error }}</div>

          <div v-if="transcript" class="preview">
            <div class="lbl">Transcript (preview)</div>
            <textarea class="textarea" rows="12" :value="transcript" readonly></textarea>
          </div>
        </div>

        <div class="card" style="margin-top: 12px;">
          <div class="page-head" style="margin-bottom: 10px;">
            <div>
              <div class="title" style="font-size: 16px;">Tap-to-time (ASS)</div>
              <div class="muted">Nhấn phím A để chốt time cho từng từ theo nhạc/giọng nói.</div>
            </div>
          </div>

          <div class="row tap-row">
            <label class="field">
              <div class="lbl">File audio/video</div>
              <input class="input" type="file" accept="audio/*,video/*" :disabled="tapRunning" @change="onPickTapMedia" />
            </label>

            <label class="field">
              <div class="lbl">Tên ASS</div>
              <input v-model="tapAssName" class="input" :disabled="tapRunning" placeholder="vd: my_subtitle" />
            </label>

            <label class="field">
              <div class="lbl">Tap mode</div>
              <div class="radio-row">
                <label class="radio-item">
                  <input v-model="tapMode" type="radio" value="word" :disabled="tapRunning" />
                  <span>Theo từ</span>
                </label>
                <label class="radio-item">
                  <input v-model="tapMode" type="radio" value="line" :disabled="tapRunning" />
                  <span>Theo dòng</span>
                </label>
              </div>
            </label>

            <label class="field">
              <div class="lbl">Hotkey</div>
              <input v-model="tapHotkey" class="input" :disabled="tapRunning" maxlength="1" />
            </label>

            <label class="field">
              <div class="lbl">Offset (ms)</div>
              <input v-model.number="tapOffsetMs" class="input" type="number" :disabled="tapRunning" />
            </label>

            <label class="field">
              <div class="lbl">Pre-roll (ms)</div>
              <input v-model.number="tapPreRollMs" class="input" type="number" :disabled="tapRunning" />
            </label>

            <label class="field">
              <div class="lbl">Lead (ms)</div>
              <input v-model.number="tapLeadMs" class="input" type="number" :disabled="tapRunning" />
            </label>
          </div>

          <div class="tap-body">
            <div class="tap-left">
              <div v-if="tapMediaUrl" class="player">
                <div v-if="tapIsVideo" class="video-stage">
                  <video ref="tapMediaEl" class="media" :src="tapMediaUrl" controls playsinline />
                  
                </div>
                <audio
                  v-else
                  ref="tapMediaEl"
                  class="media"
                  :src="tapMediaUrl"
                  controls
                />
              </div>

              <div class="actions">
                <button class="btn" type="button" :disabled="!tapMediaUrl || tapRunning || tapTargetCount === 0" @click="tapStart">
                  Start
                </button>
                <button class="btn secondary" type="button" :disabled="!tapRunning" @click="tapStop">
                  Stop
                </button>
                <button class="btn secondary" type="button" :disabled="tapRunning || tapItems.length === 0" @click="exportAss">
                  Export .ass
                </button>
                <button class="btn secondary" type="button" :disabled="tapRunning || tapItems.length === 0 || savingGeneratedAss" @click="saveGeneratedAss">
                  {{ savingGeneratedAss ? 'Saving...' : 'Save ASS' }}
                </button>
                <button class="btn secondary" type="button" :disabled="tapRunning" @click="tapReset">
                  Reset
                </button>
              </div>

              <div v-if="saveAssStatus" class="muted" style="margin-top: 8px; font-size: 12px;">{{ saveAssStatus }}</div>

              <div class="tap-status">
                <div class="muted">Progress</div>
                <div class="mono">
                  {{ tapIndex }}/{{ tapTargetCount }}
                  <span v-if="tapRunning">(running)</span>
                  <span v-else>(stopped)</span>
                </div>
                <div class="muted">Current word</div>
                <div class="mono">{{ currentTapLabel || '-' }}</div>
                <div class="muted">Current time</div>
                <div class="mono">{{ tapNow.toFixed(3) }}s</div>
              </div>
            </div>

            <div class="tap-right">
              <div class="lbl">Lyrics / lời bài hát</div>
              <textarea v-model="tapLyrics" class="textarea" rows="12" :disabled="tapRunning" placeholder="Nhập lời... (mỗi từ sẽ được gán time theo từng lần bấm hotkey)"></textarea>

              <div class="tap-preview">
                <div class="lbl">Preview</div>
                <div class="preview-lines">
                  <div v-for="(line, li) in tapLineWords" :key="li" class="preview-line">
                    <span
                      v-for="(w, wi) in line"
                      :key="wi"
                      :class="['w', isTapActiveWord(li, wi) ? 'active' : '']"
                    >
                      {{ w }}
                    </span>
                  </div>
                </div>
              </div>
            </div>
          </div>
        </div>

        <div v-if="tapError" class="error">{{ tapError }}</div>
      </div>

      <div class="page-sidebar">
        <div class="card">
          <div class="tap-style" style="margin-top: 0;">
            <div class="title" style="font-size: 16px;">ASS Style</div>

            <div class="style-row" style="margin-top: 10px;">
              <div class="muted" style="font-size: 12px;">Preset</div>
              <div class="style-grid" style="grid-template-columns: 1fr 120px 120px;">
                <label class="field">
                  <div class="lbl">AssStyle</div>
                  <select v-model="assPresetId" class="input" :disabled="tapRunning" @change="applyAssPresetById(assPresetId)">
                    <option v-for="p in assPresets" :key="p.id" :value="p.id">{{ p.name }}</option>
                  </select>
                </label>

                <div class="field" style="grid-column: span 2;">
                  <div class="lbl">Actions</div>
                  <div class="preset-actions">
                    <button class="btn secondary" type="button" :disabled="tapRunning || assPresetDbLoading" @click="refreshAssPresetsFromDb">
                      {{ assPresetDbLoading ? 'Loading...' : 'Refresh from DB' }}
                    </button>
                    <button class="btn secondary" type="button" :disabled="tapRunning" @click="loadPresetFromFile">Load from file</button>
                    <button class="btn secondary" type="button" :disabled="tapRunning || importingTemplate" @click="importTemplateFromFile">
                      {{ importingTemplate ? 'Importing...' : 'ImportTemplate' }}
                    </button>
                  </div>
                </div>
              </div>
            </div>

            <div class="style-row" style="margin-top: 10px;">
              <div class="muted" style="font-size: 12px;">Colors (Default / Transform)</div>

              <div class="color-row">
                <div class="style-grid" style="grid-template-columns: 1fr 1fr;">
                  <label class="field">
                    <div class="lbl">Default text</div>
                  </label>
                  <div class="field" style="display: flex; gap: 8px; align-items: center;">
                    <input type="color" v-model="assBasePrimaryHex" />
                    <input class="small input tiny mono" v-model="assBasePrimaryAssHex" />
                  </div>
                  <label class="field">
                    <div class="lbl">Default outline</div>
                  </label>
                  <div class="field" style="display: flex; gap: 8px; align-items: center;">
                    <input type="color" v-model="assBaseOutlineHex" />
                    <input class="small input tiny mono" v-model="assBaseOutlineAssHex" />
                  </div>

                  <label class="field">
                    <div class="lbl">Transform text</div>
                  </label>
                  <div class="field" style="display: flex; gap: 8px; align-items: center;">
                    <input type="color" v-model="assHiPrimaryHex" />
                    <input class="small input tiny mono" v-model="assHiPrimaryAssHex" />
                  </div>
                  <label class="field">
                    <div class="lbl">Transform outline</div>
                  </label>
                  <div class="field" style="display: flex; gap: 8px; align-items: center;">
                    <input type="color" v-model="assHiOutlineHex" />
                    <input class="small input tiny mono" v-model="assHiOutlineAssHex" />
                  </div>
                </div>

                <div class="preview-box color-preview">
                  <div class="muted" style="font-size: 12px;">Preview</div>
                  <div class="color-preview-line" :style="colorPreviewStyles.base">Default demo text</div>
                  <div class="color-preview-line" :style="colorPreviewStyles.hi">Transform demo text</div>
                </div>
              </div>
            </div>
          </div>
        </div>
      </div>

  </div>
</template>

<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import axios from 'axios'
import api from '@/infrastructure/http/apiClient'

const file = ref<File | null>(null)
const language = ref('')
const outputFormat = ref<'srt' | 'vtt' | 'json'>('srt')

const loading = ref(false)
const error = ref('')

const transcript = ref('')
const resultBlob = ref<Blob | null>(null)

const tapMediaFile = ref<File | null>(null)
const tapMediaUrl = ref<string>('')
const tapIsVideo = ref(false)
const tapMediaEl = ref<HTMLMediaElement | null>(null)

const tapAssName = ref('')
const tapLyrics = ref('')
const tapHotkey = ref('C')
const tapOffsetMs = ref(0)
const tapPreRollMs = ref(100)
const tapLeadMs = ref(80)

const assFontOptions = [
  'Arial',
  'Segoe UI',
  'Tahoma',
  'Verdana',
  'Times New Roman',
  'Roboto',
  'Inter',
  'Open Sans',
  'Lato',
  'Montserrat',
  'Poppins',
  'Nunito',
  'Be Vietnam Pro',
  'Oswald',
]

type AssStylePreset = {
  id: string
  name: string
  base: {
    font: string
    size: number
    bold: boolean
    italic: boolean
    primary: string
    outlineColor: string
    backColor: string
    outline: number
    shadow: number
  }
  hi: {
    font: string
    size: number
    bold: boolean
    italic: boolean
    primary: string
    outlineColor: string
    backColor: string
    outline: number
    shadow: number
  }
  layout: {
    alignment: number
    marginL: number
    marginR: number
    marginV: number
    playResX: number
    playResY: number
  }
  builtIn?: boolean
  effectText?: string // Store raw effect text from [Events]
  templateAssText?: string // If present, export this as a Karaoke Templater-ready .ass template file
  templateStyleName?: string // Original ASS Style name used by templates/karaoke lines inside templateAssText
  dbTemplateId?: string
}

const defaultAssPresets: AssStylePreset[] = [
  {
    id: 'default-gray-white',
    name: 'Default (Gray/White)',
    base: {
      font: 'Arial',
      size: 56,
      bold: false,
      italic: false,
      primary: '&H00BFBFBF',
      outlineColor: '&H00101010',
      backColor: '&H64000000',
      outline: 3,
      shadow: 0,
    },
    hi: {
      font: 'Arial',
      size: 56,
      bold: false,
      italic: false,
      primary: '&H00CCFF',
      outlineColor: '&H00101010',
      backColor: '&H64000000',
      outline: 3,
      shadow: 0,
    },
    layout: {
      alignment: 2,
      marginL: 60,
      marginR: 60,
      marginV: 60,
      playResX: 1920,
      playResY: 1080,
    },
    builtIn: true,
  },
  {
    id: 'default-yellow',
    name: 'Karaoke (Yellow)',
    base: {
      font: 'Arial',
      size: 56,
      bold: false,
      italic: false,
      primary: '&H00BFBFBF',
      outlineColor: '&H00101010',
      backColor: '&H64000000',
      outline: 3,
      shadow: 0,
    },
    hi: {
      font: 'Arial',
      size: 56,
      bold: true,
      italic: false,
      primary: '&H0000E6FF',
      outlineColor: '&H00101010',
      backColor: '&H64000000',
      outline: 3,
      shadow: 0,
    },
    layout: {
      alignment: 2,
      marginL: 60,
      marginR: 60,
      marginV: 60,
      playResX: 1920,
      playResY: 1080,
    },
    builtIn: true,
  },
]

const assPresets = ref<AssStylePreset[]>([])
const assPresetId = ref('')
const assPresetNewName = ref('')

const assPresetDbLoading = ref(false)

const assPreviewActiveIndex = ref(1)

const assBaseFont = ref('Arial')
const assBaseFontSize = ref(56)
const assBaseBold = ref(false)
const assBaseItalic = ref(false)
const assBasePrimary = ref('&H00BFBFBF')
const assBaseOutlineColor = ref('&H00101010')
const assBaseBackColor = ref('&H64000000')
const assBaseOutline = ref(3)
const assBaseShadow = ref(0)

const assHiFont = ref('Arial')
const assHiFontSize = ref(56)
const assHiBold = ref(false)
const assHiItalic = ref(false)
const assHiPrimary = ref('&H00CCFF')
const assHiOutlineColor = ref('&H00101010')
const assHiBackColor = ref('&H64000000')
const assHiOutline = ref(3)
const assHiShadow = ref(0)
const assAlignment = ref(2)
const assMarginL = ref(60)
const assMarginR = ref(60)
const assMarginV = ref(60)
const assPlayResX = ref(1920)
const assPlayResY = ref(1080)

const importingTemplate = ref(false)

const loadStatus = ref('')
const lastLoadedAssText = ref('')

const saveAssStatus = ref('')
const savingGeneratedAss = ref(false)

const assColorToCss = (ass: string) => {
  const s = String(ass ?? '').trim().toUpperCase()
  const cleaned = s.endsWith('&') ? s.slice(0, -1) : s

  const m8 = cleaned.match(/^&H([0-9A-F]{2})([0-9A-F]{2})([0-9A-F]{2})([0-9A-F]{2})$/)
  if (m8) {
    const aa = parseInt(m8[1], 16)
    const bb = parseInt(m8[2], 16)
    const gg = parseInt(m8[3], 16)
    const rr = parseInt(m8[4], 16)
    const a = Math.max(0, Math.min(1, (255 - aa) / 255))
    return `rgba(${rr},${gg},${bb},${a.toFixed(3)})`
  }

  const m6 = cleaned.match(/^&H([0-9A-F]{2})([0-9A-F]{2})([0-9A-F]{2})$/)
  if (m6) {
    const bb = parseInt(m6[1], 16)
    const gg = parseInt(m6[2], 16)
    const rr = parseInt(m6[3], 16)
    return `rgba(${rr},${gg},${bb},1)`
  }

  return 'rgba(255,255,255,1)'
}

const makeOutlineTextShadow = (outlineCss: string) => {
  const c = String(outlineCss ?? '')
  return [
    `-1px -1px 0 ${c}`,
    `0 -1px 0 ${c}`,
    `1px -1px 0 ${c}`,
    `-1px 0 0 ${c}`,
    `1px 0 0 ${c}`,
    `-1px 1px 0 ${c}`,
    `0 1px 0 ${c}`,
    `1px 1px 0 ${c}`,
  ].join(',')
}

const colorPreviewStyles = computed(() => {
  const baseColor = assColorToCss(assBasePrimary.value)
  const baseOutline = assColorToCss(assBaseOutlineColor.value)
  const hiColor = assColorToCss(assHiPrimary.value)
  const hiOutline = assColorToCss(assHiOutlineColor.value)

  return {
    base: {
      color: baseColor,
      textShadow: makeOutlineTextShadow(baseOutline),
      fontWeight: 800,
    } as Record<string, any>,
    hi: {
      color: hiColor,
      textShadow: makeOutlineTextShadow(hiOutline),
      fontWeight: 900,
    } as Record<string, any>,
  }
})

const parseAssOverrideTags = (tagText: string) => {
  const t = String(tagText ?? '')
  const getColor = (re: RegExp) => {
    const m = t.match(re)
    if (!m) return null
    const raw = m[1]?.toUpperCase() ?? ''
    const full = raw.length === 8 ? `&H${raw}` : raw.length === 6 ? `&H00${raw}` : raw
    if (!full.match(/^&H[0-9A-F]{8}$/)) return null
    return full
  }
  const getNum = (re: RegExp) => {
    const m = t.match(re)
    if (!m) return null
    const v = Number(m[1])
    return Number.isFinite(v) ? v : null
  }
  // Match literal ASS tags e.g. "\1c&H...&" inside override blocks
  // IMPORTANT: avoid regex backreference ambiguity like /\1/ by using (?:...) after the literal backslash.
  const primary = getColor(/\\(?:1c)&H([0-9A-F]{6,8})&/i)
  const outlineColor = getColor(/\\(?:3c)&H([0-9A-F]{6,8})&/i)
  const fs = getNum(/\\(?:fs)([0-9]+(?:\.[0-9]+)?)/i)
  const bord = getNum(/\\(?:bord)([0-9]+(?:\.[0-9]+)?)/i)
  const an = getNum(/\\(?:an)([1-9])/i)
  const alpha = getColor(/\\(?:alpha)&H([0-9A-F]{2})&/i)
  const fscx = getNum(/\\(?:fscx)([0-9]+(?:\.[0-9]+)?)/i)
  const fscy = getNum(/\\(?:fscy)([0-9]+(?:\.[0-9]+)?)/i)
  return { primary, outlineColor, fs, bord, an, alpha, fscx, fscy }
}

type AssInspectorStyle = {
  name: string
  fontname: string
  fontsize: number
  primary: string
  outlineColor: string
  outline: number
  alignment: number
  marginV: number
  primaryCss: string
  outlineCss: string
}

type AssInspectorEvent = {
  kind: 'Comment'
  start: string
  end: string
  style: string
  effect: string
  text: string
  transforms: Array<{ t0: number | null; t1: number | null; summary: string }>
  resolved: {
    primary: string
    outlineColor: string
    fs: number
    bord: number
    an: number
    primaryCss: string
    outlineCss: string
  }
}

const parseAssForInspector = (text: string) => {
  const raw = String(text ?? '').replace(/\r\n/g, '\n')
  const lines = raw.split('\n')

  let inStyles = false
  let inEvents = false
  let styleFormat: string[] | null = null
  let eventFormat: string[] | null = null
  const styles: AssInspectorStyle[] = []
  const stylesByName = new Map<string, AssInspectorStyle>()
  const events: AssInspectorEvent[] = []

  const removeAllAssTransformsForInspector = (tagText: string) => {
    const s = String(tagText ?? '')
    let out = ''
    for (let i = 0; i < s.length; i++) {
      if (s[i] === '\\' && s[i + 1] === 't' && s[i + 2] === '(') {
        i += 3
        let depth = 1
        for (; i < s.length; i++) {
          const ch = s[i]
          if (ch === '(') depth++
          else if (ch === ')') {
            depth--
            if (depth === 0) break
          }
        }
        continue
      }
      out += s[i]
    }
    return out
  }

  const extractAssTransformsForInspector = (tagText: string) => {
    const s = String(tagText ?? '')
    const res: string[] = []
    for (let i = 0; i < s.length; i++) {
      if (s[i] === '\\' && s[i + 1] === 't' && s[i + 2] === '(') {
        const start = i
        i += 3
        let depth = 1
        for (; i < s.length; i++) {
          const ch = s[i]
          if (ch === '(') depth++
          else if (ch === ')') {
            depth--
            if (depth === 0) {
              res.push(s.slice(start, i + 1))
              break
            }
          }
        }
      }
    }
    return res
  }

  const parseTransformBlock = (block: string) => {
    const b = String(block ?? '').trim()
    const m = b.match(/^\\t\((.*)\)$/i)
    const inner = m ? m[1] : ''
    if (!inner) return { t0: null as number | null, t1: null as number | null, tagText: '' }

    const parts: string[] = []
    let cur = ''
    let depth = 0
    for (let i = 0; i < inner.length; i++) {
      const ch = inner[i]
      if (ch === '(') depth++
      else if (ch === ')') depth = Math.max(0, depth - 1)
      if (ch === ',' && depth === 0) {
        parts.push(cur)
        cur = ''
        continue
      }
      cur += ch
    }
    parts.push(cur)
    const nums = parts.map((x) => String(x ?? '').trim())

    const tryNum = (s: string) => {
      const v = Number(s)
      return Number.isFinite(v) ? v : null
    }

    const t0 = nums.length >= 2 ? tryNum(nums[0]) : null
    const t1 = nums.length >= 2 ? tryNum(nums[1]) : null
    const tagText = nums.length >= 3 ? nums.slice(2).join(',') : ''
    return { t0, t1, tagText }
  }

  const idxOf = (fmt: string[] | null, key: string) => {
    if (!fmt) return -1
    return fmt.findIndex((x) => x.trim().toLowerCase() === key.toLowerCase())
  }

  for (const line of lines) {
    const t = line.trim()
    if (!t) continue

    if (t.startsWith('[') && t.endsWith(']')) {
      const sec = t.toLowerCase()
      inStyles = sec === '[v4+ styles]' || sec === '[v4 styles]'
      inEvents = sec === '[events]'
      continue
    }

    if (inStyles) {
      if (t.toLowerCase().startsWith('format:')) {
        styleFormat = t.slice('format:'.length).split(',').map((x) => x.trim())
        continue
      }
      if (t.toLowerCase().startsWith('style:')) {
        const parts = t.slice('style:'.length).split(',').map((x) => x.trim())
        const name = parts[0] || ''
        if (!name) continue
        const getStr = (k: string, fb: string) => {
          const i = idxOf(styleFormat, k)
          const v = i >= 0 ? String(parts[i] ?? '').trim() : ''
          return v || fb
        }
        const getNum = (k: string, fb: number) => {
          const i = idxOf(styleFormat, k)
          const v = i >= 0 ? Number(parts[i] ?? '') : NaN
          return Number.isFinite(v) ? v : fb
        }

        const primary = normalizeAssColor(getStr('PrimaryColour', '&H00FFFFFF'), '&H00FFFFFF')
        const outlineColor = normalizeAssColor(getStr('OutlineColour', '&H00101010'), '&H00101010')
        const sObj: AssInspectorStyle = {
          name,
          fontname: getStr('Fontname', 'Arial'),
          fontsize: getNum('Fontsize', 56),
          primary,
          outlineColor,
          outline: getNum('Outline', 2),
          alignment: getNum('Alignment', 2),
          marginV: getNum('MarginV', 60),
          primaryCss: assColorToCss(primary),
          outlineCss: assColorToCss(outlineColor),
        }
        styles.push(sObj)
        stylesByName.set(name, sObj)
        continue
      }
    }

    if (inEvents) {
      if (t.toLowerCase().startsWith('format:')) {
        eventFormat = t.slice('format:'.length).split(',').map((x) => x.trim())
        continue
      }

      const lower = t.toLowerCase()
      if (!lower.startsWith('comment:')) continue
      const kind: 'Comment' = 'Comment'
      const payload = t.replace(/^comment\s*:\s*/i, '')

      // If we don't know format, fallback to default split (first 9 commas)
      const fields = splitAssEventFields(payload)

      const styleName = String(fields.style ?? '').trim()
      const styleObj = stylesByName.get(styleName)

      const rawText = String(fields.text ?? '')
      const noBang = stripTemplaterBangExpr(rawText)
      const inside = extractFirstBraceBlock(noBang)
      const baseOnly = removeAllAssTransformsForInspector(inside)
      const tags = parseAssOverrideTags(baseOnly)

      const transformBlocks = extractAssTransformsForInspector(inside)
      const transforms = transformBlocks
        .map((b) => {
          const parsed = parseTransformBlock(b)
          const tt = parseAssOverrideTags(parsed.tagText)
          const bits: string[] = []
          if (tt.primary) bits.push(`1c ${tt.primary}`)
          if (tt.alpha) bits.push(`alpha ${tt.alpha}`)
          if (tt.fscx != null) bits.push(`fscx ${tt.fscx}`)
          if (tt.fscy != null) bits.push(`fscy ${tt.fscy}`)
          const range = parsed.t0 != null && parsed.t1 != null ? `${parsed.t0}-${parsed.t1}ms` : 't'
          return { t0: parsed.t0, t1: parsed.t1, summary: `${range}: ${bits.join(' / ') || '-'}` }
        })
        .filter((x) => x.summary)

      const resolvedPrimary = tags.primary ?? styleObj?.primary ?? '&H00FFFFFF'
      const resolvedOutlineColor = tags.outlineColor ?? styleObj?.outlineColor ?? '&H00101010'
      const resolvedFs = tags.fs ?? styleObj?.fontsize ?? 56
      const resolvedBord = tags.bord ?? styleObj?.outline ?? 2
      const resolvedAn = tags.an ?? styleObj?.alignment ?? 2

      events.push({
        kind,
        start: String(fields.start ?? ''),
        end: String(fields.end ?? ''),
        style: styleName,
        effect: String(fields.effect ?? ''),
        text: rawText,
        transforms,
        resolved: {
          primary: resolvedPrimary,
          outlineColor: resolvedOutlineColor,
          fs: resolvedFs,
          bord: resolvedBord,
          an: resolvedAn,
          primaryCss: assColorToCss(resolvedPrimary),
          outlineCss: assColorToCss(resolvedOutlineColor),
        },
      })
    }
  }

  return { styles, events }
}

const assInspectorParsed = computed(() => {
  const raw = String(lastLoadedAssText.value ?? '').trim()
  if (!raw) return null
  try {
    return parseAssForInspector(raw)
  } catch {
    return null
  }
})

const extractFirstBraceBlock = (s: string) => {
  const raw = String(s ?? '')
  const i0 = raw.indexOf('{')
  if (i0 < 0) return ''
  let depth = 0
  for (let i = i0; i < raw.length; i++) {
    const ch = raw[i]
    if (ch === '{') depth++
    else if (ch === '}') {
      depth--
      if (depth === 0) return raw.slice(i0 + 1, i)
    }
  }
  return raw.slice(i0 + 1)
}

const stripTemplaterBangExpr = (s: string) => String(s ?? '').replace(/![^!]*!/g, '')

const assToHex = (ass: string) => {
  const s = String(ass ?? '').trim().toUpperCase()
  const m8 = s.match(/&H([0-9A-F]{2})([0-9A-F]{2})([0-9A-F]{2})([0-9A-F]{2})/)
  if (m8) {
    const bb = m8[2]
    const gg = m8[3]
    const rr = m8[4]
    return `#${rr}${gg}${bb}`
  }
  const m6 = s.match(/&H([0-9A-F]{2})([0-9A-F]{2})([0-9A-F]{2})/)
  if (!m6) return '#FFFFFF'
  const bb = m6[1]
  const gg = m6[2]
  const rr = m6[3]
  return `#${rr}${gg}${bb}`
}

const normalizeAssHexInput = (input: string, prevAss: string) => {
  const raw = String(input ?? '').trim().toUpperCase()
  const cleaned = raw.endsWith('&') ? raw.slice(0, -1) : raw
  if (!cleaned.startsWith('&H')) return String(prevAss ?? '').trim()
  const hex = cleaned.slice(2).replace(/[^0-9A-F]/g, '')
  if (hex.length === 6) return `&H${hex}`
  if (hex.length === 8) return `&H${hex}`
  return String(prevAss ?? '').trim()
}

const toAssHexDisplay = (ass: string) => {
  const s = String(ass ?? '').trim().toUpperCase()
  if (!s) return ''
  return s.startsWith('&H') ? s : `&H${s.replace(/^H/i, '')}`
}

const hexToAssKeepAlpha = (hex: string, prevAss: string) => {
  const prev = String(prevAss ?? '').trim().toUpperCase()
  const alphaMatch = prev.match(/&H([0-9A-F]{2})[0-9A-F]{6}/)
  const aa = alphaMatch?.[1] ?? '00'

  const h = String(hex ?? '').trim()
  const mm = h.match(/^#?([0-9A-Fa-f]{6})$/)
  const rgb = mm ? mm[1].toUpperCase() : 'FFFFFF'
  const rr = rgb.slice(0, 2)
  const gg = rgb.slice(2, 4)
  const bb = rgb.slice(4, 6)
  return `&H${aa}${bb}${gg}${rr}`
}

const assBasePrimaryHex = computed({
  get: () => assToHex(assBasePrimary.value),
  set: (v: string) => {
    assBasePrimary.value = hexToAssKeepAlpha(v, assBasePrimary.value)
  },
})

const assBaseOutlineHex = computed({
  get: () => assToHex(assBaseOutlineColor.value),
  set: (v: string) => {
    assBaseOutlineColor.value = hexToAssKeepAlpha(v, assBaseOutlineColor.value)
  },
})

const assBaseBackHex = computed({
  get: () => assToHex(assBaseBackColor.value),
  set: (v: string) => {
    assBaseBackColor.value = hexToAssKeepAlpha(v, assBaseBackColor.value)
  },
})

const assHiPrimaryHex = computed({
  get: () => assToHex(assHiPrimary.value),
  set: (v: string) => {
    assHiPrimary.value = hexToAssKeepAlpha(v, assHiPrimary.value)
  },
})

const assHiOutlineHex = computed({
  get: () => assToHex(assHiOutlineColor.value),
  set: (v: string) => {
    assHiOutlineColor.value = hexToAssKeepAlpha(v, assHiOutlineColor.value)
  },
})

const assBasePrimaryAssHex = computed({
  get: () => toAssHexDisplay(assBasePrimary.value),
  set: (v: string) => {
    assBasePrimary.value = normalizeAssHexInput(v, assBasePrimary.value)
  },
})

const assBaseOutlineAssHex = computed({
  get: () => toAssHexDisplay(assBaseOutlineColor.value),
  set: (v: string) => {
    assBaseOutlineColor.value = normalizeAssHexInput(v, assBaseOutlineColor.value)
  },
})

const assHiPrimaryAssHex = computed({
  get: () => toAssHexDisplay(assHiPrimary.value),
  set: (v: string) => {
    assHiPrimary.value = normalizeAssHexInput(v, assHiPrimary.value)
  },
})

const assHiOutlineAssHex = computed({
  get: () => toAssHexDisplay(assHiOutlineColor.value),
  set: (v: string) => {
    assHiOutlineColor.value = normalizeAssHexInput(v, assHiOutlineColor.value)
  },
})

const assHiBackHex = computed({
  get: () => assToHex(assHiBackColor.value),
  set: (v: string) => {
    assHiBackColor.value = hexToAssKeepAlpha(v, assHiBackColor.value)
  },
})

const canDeleteSelectedPreset = computed(() => {
  const p = assPresets.value.find((x) => x.id === assPresetId.value)
  if (!p) return false
  return !p.builtIn
})

const getPresetById = (id: string) => assPresets.value.find((x) => x.id === id) ?? null

const assToRgba = (ass: string) => {
  const s = String(ass ?? '').trim().toUpperCase()
  const m8 = s.match(/&H([0-9A-F]{2})([0-9A-F]{2})([0-9A-F]{2})([0-9A-F]{2})/)
  if (m8) {
    const aa = parseInt(m8[1], 16)
    const bb = parseInt(m8[2], 16)
    const gg = parseInt(m8[3], 16)
    const rr = parseInt(m8[4], 16)
    const a = Math.max(0, Math.min(1, 1 - aa / 255))
    return `rgba(${rr},${gg},${bb},${a.toFixed(3)})`
  }

  const m6 = s.match(/&H([0-9A-F]{2})([0-9A-F]{2})([0-9A-F]{2})/)
  if (!m6) return 'rgba(255,255,255,1)'
  const bb = parseInt(m6[1], 16)
  const gg = parseInt(m6[2], 16)
  const rr = parseInt(m6[3], 16)
  return `rgba(${rr},${gg},${bb},1)`
}

const makeCssTextShadow = (outline: number, shadow: number, outlineColor: string) => {
  const o = Math.max(0, Number(outline || 0))
  const s = Math.max(0, Number(shadow || 0))
  const col = assToRgba(outlineColor)
  const parts: string[] = []
  if (o > 0) {
    parts.push(`${o}px 0 ${col}`)
    parts.push(`${-o}px 0 ${col}`)
    parts.push(`0 ${o}px ${col}`)
    parts.push(`0 ${-o}px ${col}`)
    parts.push(`${o}px ${o}px ${col}`)
    parts.push(`${-o}px ${o}px ${col}`)
    parts.push(`${o}px ${-o}px ${col}`)
    parts.push(`${-o}px ${-o}px ${col}`)
  }
  if (s > 0) parts.push(`${s}px ${s}px rgba(0,0,0,0.6)`)
  return parts.join(', ')
}

const presetToPreviewStyles = (p: AssStylePreset | null) => {
  if (!p) {
    return {
      base: {},
      hi: {},
    }
  }

  const base = {
    fontFamily: p.base.font,
    fontSize: `${p.base.size}px`,
    fontWeight: p.base.bold ? '800' : '400',
    fontStyle: p.base.italic ? 'italic' : 'normal',
    color: assToRgba(p.base.primary),
    textShadow: makeCssTextShadow(p.base.outline, p.base.shadow, p.base.outlineColor),
  } as Record<string, string>

  const hi = {
    fontFamily: p.hi.font,
    fontSize: `${p.hi.size}px`,
    fontWeight: p.hi.bold ? '800' : '400',
    fontStyle: p.hi.italic ? 'italic' : 'normal',
    color: assToRgba(p.hi.primary),
    textShadow: makeCssTextShadow(p.hi.outline, p.hi.shadow, p.hi.outlineColor),
  } as Record<string, string>

  return { base, hi }
}

const previewStyle = computed(() => {
  const cur = getCurrentAssPresetSnapshot()
  return presetToPreviewStyles(cur)
})

const subtitleOverlayPosStyle = computed(() => {
  const a = Number(assAlignment.value || 2)
  const ml = Math.max(0, Number(assMarginL.value || 0))
  const mr = Math.max(0, Number(assMarginR.value || 0))
  const mv = Math.max(0, Number(assMarginV.value || 0))

  const isTop = a >= 7 && a <= 9
  const isMid = a >= 4 && a <= 6
  const isBot = a >= 1 && a <= 3

  const isLeft = a === 1 || a === 4 || a === 7
  const isCenter = a === 2 || a === 5 || a === 8
  const isRight = a === 3 || a === 6 || a === 9

  const justifyContent = isLeft ? 'flex-start' : isRight ? 'flex-end' : 'center'
  const alignItems = isTop ? 'flex-start' : isMid ? 'center' : 'flex-end'

  const paddingTop = isTop ? `${mv}px` : '0px'
  const paddingBottom = isBot ? `${mv}px` : '0px'

  return {
    justifyContent,
    alignItems,
    paddingLeft: `${ml}px`,
    paddingRight: `${mr}px`,
    paddingTop,
    paddingBottom,
  } as Record<string, string>
})

const tapRunning = ref(false)
const tapError = ref('')

type TapItem = {
  w: string
  start: number
  end: number
}

type TapMergedItem = {
  w: string
  start: number
  end: number
  count: number
}

const mergeAdjacentSameWords = (items: TapItem[]): TapMergedItem[] => {
  const out: TapMergedItem[] = []
  for (const cur of items) {
    const w = String(cur.w ?? '').trim()
    if (out.length === 0) {
      out.push({ w, start: cur.start, end: cur.end, count: 1 })
      continue
    }

    const last = out[out.length - 1]!
    if (String(last.w ?? '').trim() === w) {
      last.end = Math.max(last.end, cur.end)
      last.count = Math.max(1, Math.floor(Number(last.count ?? 1))) + 1
    } else {
      out.push({ w, start: cur.start, end: cur.end, count: 1 })
    }
  }
  return out
}

const stripAssTransformTags = (input: string) => {
  const s = String(input ?? '')
  let out = ''
  let i = 0
  while (i < s.length) {
    const idx = s.indexOf('\\t(', i)
    if (idx < 0) {
      out += s.slice(i)
      break
    }

    // If we see an escaped transform tag (e.g. "\\\\t(" inside templater code), keep it.
    // Example: the substring "\\t(" contains "\\t(" starting from the 2nd backslash.
    if (idx > 0 && s[idx - 1] === '\\') {
      out += s.slice(i, idx)
      out += '\\'
      i = idx + 1
      continue
    }

    out += s.slice(i, idx)

    let j = idx + 3
    let depth = 1
    while (j < s.length && depth > 0) {
      const ch = s[j]
      if (ch === '(') depth++
      else if (ch === ')') depth--
      j++
    }

    i = j
  }
  return out
}

const tapIndex = ref(0)
const tapItems = ref<TapItem[]>([])
const tapNow = ref(0)
let tapRaf: number | null = null
const tapPressTimes = ref<number[]>([])

const tapMode = ref<'word' | 'line'>('word')

type TapWordMeta = {
  w: string
  line: number
  wIndexInLine: number
}

const tapLyricLines = computed(() => {
  const raw = (tapLyrics.value ?? '').replace(/\r\n/g, '\n')
  const lines = raw.split('\n')
  return lines.map((x) => x.trim()).filter((x) => x.length > 0)
})

const tapLineWords = computed(() => {
  return tapLyricLines.value.map((line) => line.split(/\s+/g).filter(Boolean))
})

const tapWordMeta = computed<TapWordMeta[]>(() => {
  const metas: TapWordMeta[] = []
  for (let li = 0; li < tapLineWords.value.length; li++) {
    const ws = tapLineWords.value[li]
    for (let wi = 0; wi < ws.length; wi++) {
      metas.push({ w: ws[wi], line: li, wIndexInLine: wi })
    }
  }
  return metas
})

const tapTargetCount = computed(() => (tapMode.value === 'line' ? tapLyricLines.value.length : tapWordMeta.value.length))

const currentTapLabel = computed(() => {
  if (tapMode.value === 'line') return tapLyricLines.value[tapIndex.value] ?? ''
  return tapWordMeta.value[tapIndex.value]?.w ?? ''
})

const isTapActiveWord = (li: number, wi: number) => {
  if (tapMode.value === 'line') return tapIndex.value === li
  return tapWordMeta.value[tapIndex.value]?.line === li && tapWordMeta.value[tapIndex.value]?.wIndexInLine === wi
}

watch(tapMediaUrl, (next, prev) => {
  if (prev && prev !== next) URL.revokeObjectURL(prev)
})

const getCurrentAssPresetSnapshot = (): AssStylePreset => {
  return {
    id: assPresetId.value || 'current',
    name: 'Current',
    base: {
      font: assBaseFont.value,
      size: Number(assBaseFontSize.value || 0),
      bold: Boolean(assBaseBold.value),
      italic: Boolean(assBaseItalic.value),
      primary: assBasePrimary.value,
      outlineColor: assBaseOutlineColor.value,
      backColor: assBaseBackColor.value,
      outline: Number(assBaseOutline.value || 0),
      shadow: Number(assBaseShadow.value || 0),
    },
    hi: {
      font: assHiFont.value,
      size: Number(assHiFontSize.value || 0),
      bold: Boolean(assHiBold.value),
      italic: Boolean(assHiItalic.value),
      primary: assHiPrimary.value,
      outlineColor: assHiOutlineColor.value,
      backColor: assHiBackColor.value,
      outline: Number(assHiOutline.value || 0),
      shadow: Number(assHiShadow.value || 0),
    },
    layout: {
      alignment: Number(assAlignment.value || 2),
      marginL: Number(assMarginL.value || 0),
      marginR: Number(assMarginR.value || 0),
      marginV: Number(assMarginV.value || 0),
      playResX: Number(assPlayResX.value || 0),
      playResY: Number(assPlayResY.value || 0),
    },
  }
}

const applyAssPreset = (p: AssStylePreset) => {
  assBaseFont.value = p.base.font
  assBaseFontSize.value = p.base.size
  assBaseBold.value = p.base.bold
  assBaseItalic.value = p.base.italic
  assBasePrimary.value = p.base.primary
  assBaseOutlineColor.value = p.base.outlineColor
  assBaseBackColor.value = p.base.backColor
  assBaseOutline.value = p.base.outline
  assBaseShadow.value = p.base.shadow

  assHiFont.value = p.hi.font
  assHiFontSize.value = p.hi.size
  assHiBold.value = p.hi.bold
  assHiItalic.value = p.hi.italic
  assHiPrimary.value = p.hi.primary
  assHiOutlineColor.value = p.hi.outlineColor
  assHiBackColor.value = p.hi.backColor
  assHiOutline.value = p.hi.outline
  assHiShadow.value = p.hi.shadow

  assAlignment.value = p.layout.alignment
  assMarginL.value = p.layout.marginL
  assMarginR.value = p.layout.marginR
  assMarginV.value = p.layout.marginV
  assPlayResX.value = p.layout.playResX
  assPlayResY.value = p.layout.playResY
}

const applyAssPresetById = (id: string) => {
  const p = assPresets.value.find((x) => x.id === id)
  if (!p) return
  applyAssPreset(p)
}

type DbAssTemplateDto = {
  id: string
  name: string
  tags?: string | null
  content: string
  isActive: boolean
  createdAtUtc: string
  updatedAtUtc: string
}

const loadAssPresetsFromDb = async () => {
  const templates = await api.get<DbAssTemplateDto[]>('admin/ass-templates?isActive=true')
  const all: AssStylePreset[] = []

  for (const t of (templates ?? [])) {
    const content = String(t?.content ?? '').trim()
    if (!content) continue

    const parsed = parseAssTemplateToPresets(content)
    for (const p of parsed) {
      const style = (p.templateStyleName ?? '').trim() || 'Default'
      all.push({
        ...p,
        id: `${t.id}:${style}`,
        name: `${t.name} - ${style}`,
        dbTemplateId: t.id,
      })
    }
  }

  return all
}

const refreshAssPresetsFromDb = async () => {
  try {
    assPresetDbLoading.value = true
    const fromDb = await loadAssPresetsFromDb()
    assPresets.value = fromDb && fromDb.length ? fromDb : defaultAssPresets
    assPresetId.value = assPresets.value[0]?.id ?? ''
    if (assPresetId.value) applyAssPresetById(assPresetId.value)
  } finally {
    assPresetDbLoading.value = false
  }
}

const normalizeAssPreset = (raw: any): AssStylePreset | null => {
  if (!raw || typeof raw !== 'object') return null
  const baseFallback = defaultAssPresets[0]!.base
  const hiFallback = defaultAssPresets[0]!.hi
  const layoutFallback = defaultAssPresets[0]!.layout

  const id = typeof raw.id === 'string' && raw.id.trim() ? raw.id : `custom-${Date.now()}`
  const name = typeof raw.name === 'string' && raw.name.trim() ? raw.name : 'Unnamed'

  const baseRaw = raw.base ?? {}
  const hiRaw = raw.hi ?? {}
  const layoutRaw = raw.layout ?? {}

  const base = {
    font: typeof baseRaw.font === 'string' ? baseRaw.font : baseFallback.font,
    size: Number.isFinite(Number(baseRaw.size)) ? Number(baseRaw.size) : baseFallback.size,
    bold: Boolean(baseRaw.bold),
    italic: Boolean(baseRaw.italic),
    primary: typeof baseRaw.primary === 'string' ? baseRaw.primary : baseFallback.primary,
    outlineColor: typeof baseRaw.outlineColor === 'string' ? baseRaw.outlineColor : baseFallback.outlineColor,
    backColor: typeof baseRaw.backColor === 'string' ? baseRaw.backColor : baseFallback.backColor,
    outline: Number.isFinite(Number(baseRaw.outline)) ? Number(baseRaw.outline) : baseFallback.outline,
    shadow: Number.isFinite(Number(baseRaw.shadow)) ? Number(baseRaw.shadow) : baseFallback.shadow,
  }

  const hi = {
    font: typeof hiRaw.font === 'string' ? hiRaw.font : hiFallback.font,
    size: Number.isFinite(Number(hiRaw.size)) ? Number(hiRaw.size) : hiFallback.size,
    bold: Boolean(hiRaw.bold),
    italic: Boolean(hiRaw.italic),
    primary: typeof hiRaw.primary === 'string' ? hiRaw.primary : hiFallback.primary,
    outlineColor: typeof hiRaw.outlineColor === 'string' ? hiRaw.outlineColor : hiFallback.outlineColor,
    backColor: typeof hiRaw.backColor === 'string' ? hiRaw.backColor : hiFallback.backColor,
    outline: Number.isFinite(Number(hiRaw.outline)) ? Number(hiRaw.outline) : hiFallback.outline,
    shadow: Number.isFinite(Number(hiRaw.shadow)) ? Number(hiRaw.shadow) : hiFallback.shadow,
  }

  const layout = {
    alignment: Number.isFinite(Number(layoutRaw.alignment)) ? Number(layoutRaw.alignment) : layoutFallback.alignment,
    marginL: Number.isFinite(Number(layoutRaw.marginL)) ? Number(layoutRaw.marginL) : layoutFallback.marginL,
    marginR: Number.isFinite(Number(layoutRaw.marginR)) ? Number(layoutRaw.marginR) : layoutFallback.marginR,
    marginV: Number.isFinite(Number(layoutRaw.marginV)) ? Number(layoutRaw.marginV) : layoutFallback.marginV,
    playResX: Number.isFinite(Number(layoutRaw.playResX)) ? Number(layoutRaw.playResX) : layoutFallback.playResX,
    playResY: Number.isFinite(Number(layoutRaw.playResY)) ? Number(layoutRaw.playResY) : layoutFallback.playResY,
  }

  const builtIn = typeof raw.builtIn === 'boolean' ? raw.builtIn : false
  return { id, name, base, hi, layout, builtIn }
}

const saveAssPresets = async () => {
  return
}

const savePresetToFile = async (preset: AssStylePreset) => {
  const ass = buildAssFromPreset(preset)
  const blob = new Blob([ass], { type: 'text/plain;charset=utf-8' })
  const filename = `${sanitizeFileName(preset.name)}.ass`
  // Save using File System Access API if available, otherwise download
  if ('showSaveFilePicker' in window) {
    try {
      const handle = await (window as any).showSaveFilePicker({
        suggestedName: filename,
        types: [{ description: 'ASS files', accept: { 'text/plain': ['.ass'] } }],
      })
      const writable = await handle.createWritable()
      await writable.write(blob)
      await writable.close()
      return
    } catch {}
  }
  // Fallback: download
  const url = URL.createObjectURL(blob)
  const a = document.createElement('a')
  a.href = url
  a.download = filename
  a.click()
  setTimeout(() => URL.revokeObjectURL(url), 1000)
}

const saveGeneratedAss = async () => {
  saveAssStatus.value = ''
  tapError.value = ''

  if (tapItems.value.length === 0) {
    tapError.value = 'Chưa có timing nào.'
    return
  }

  try {
    savingGeneratedAss.value = true
    const content = buildBasicAssForSaving()
    const assName = (tapAssName.value ?? '').trim()
    const fallback = (tapMediaFile.value?.name ?? 'subtitle').trim() || 'subtitle'
    const sourceFileName = assName || fallback

    await api.post('admin/generated-subtitles', {
      sourceFileName,
      assTemplateId: null,
      assText: content,
    })

    saveAssStatus.value = 'Saved.'
  } catch (e: any) {
    saveAssStatus.value = `Save failed: ${e?.message ?? e}`
  } finally {
    savingGeneratedAss.value = false
  }
}

const sanitizeFileName = (name: string) => {
  return name.replace(/[^a-z0-9\u00C0-\u024F\-_\s]/gi, '').trim() || 'preset'
}

const buildAssFromPreset = (preset: AssStylePreset) => {
  if (preset.templateAssText && String(preset.templateAssText).trim()) {
    return String(preset.templateAssText).replace(/\r\n/g, '\n')
  }
  const lines = []
  lines.push('[Script Info]')
  lines.push(`PlayResX: ${preset.layout.playResX}`)
  lines.push(`PlayResY: ${preset.layout.playResY}`)
  lines.push('')
  lines.push('[V4+ Styles]')
  lines.push('Format: Name,Fontname,Fontsize,PrimaryColour,SecondaryColour,OutlineColour,BackColour,Bold,Italic,Underline,StrikeOut,ScaleX,ScaleY,Spacing,Angle,BorderStyle,Outline,Shadow,Alignment,MarginL,MarginR,MarginV,Encoding')
  const style = (name: string, style: any) => {
    const fields = [
      name,
      style.font,
      style.size,
      style.primary,
      '&H000000FF',
      style.outlineColor,
      style.backColor,
      style.bold ? -1 : 0,
      style.italic ? -1 : 0,
      0,
      0,
      100,
      100,
      0,
      0,
      1,
      style.outline,
      style.shadow,
      preset.layout.alignment,
      preset.layout.marginL,
      preset.layout.marginR,
      preset.layout.marginV,
      1,
    ]
    return `Style: ${fields.join(',')}`
  }
  lines.push(style(preset.name, preset.base))
  if (preset.hi.font !== preset.base.font || preset.hi.size !== preset.base.size || preset.hi.primary !== preset.base.primary || preset.hi.outlineColor !== preset.base.outlineColor || preset.hi.backColor !== preset.base.backColor || preset.hi.bold !== preset.base.bold || preset.hi.italic !== preset.base.italic || preset.hi.outline !== preset.base.outline || preset.hi.shadow !== preset.base.shadow) {
    lines.push(style(preset.name + '-Hi', preset.hi))
  }
  lines.push('')
  lines.push('[Events]')
  lines.push('Format: Layer,Start,End,Style,Name,MarginL,MarginR,MarginV,Effect,Text')
  if (preset.effectText && preset.effectText.startsWith('template')) {
    // Include template as comment and sample usage
    lines.push(`! Template: ${preset.effectText}`)
    lines.push(`Dialogue: 0,0:00:00.00,0:00:05.00,${preset.name},,0,0,0,,{${preset.effectText}}Sample`)
  } else {
    // Fallback generic sample
    lines.push(`Dialogue: 0,0:00:00.00,0:00:05.00,${preset.name},,0,0,0,,Sample text`)
    if (preset.hi.font !== preset.base.font || preset.hi.size !== preset.base.size || preset.hi.primary !== preset.base.primary || preset.hi.outlineColor !== preset.base.outlineColor || preset.hi.backColor !== preset.base.backColor || preset.hi.bold !== preset.base.bold || preset.hi.italic !== preset.base.italic || preset.hi.outline !== preset.base.outline || preset.hi.shadow !== preset.base.shadow) {
      lines.push(`Dialogue: 0,0:00:00.00,0:00:05.00,${preset.name}-Hi,,0,0,0,,{\\k1}Sample`)
    }
  }
  return lines.join('\n')
}

const parseAssFileToPreset = (text: string, nameHint: string): AssStylePreset | null => {
  const raw = String(text ?? '').replace(/\r\n/g, '\n')
  const lines = raw.split('\n')

  let playResX: number | null = null
  let playResY: number | null = null
  let inScriptInfo = false
  let inStyles = false
  let inEvents = false
  let styleFormat: string[] | null = null
  let stylesFormatLine: string | null = null
  const stylesMap = new Map<string, string[]>()
  const styleLinesRaw: string[] = []

  const events: Array<{ raw: string; style: string; effect: string; isComment: boolean }> = []

  for (const line of lines) {
    const t = line.trim()
    if (!t) continue

    if (t.startsWith('[') && t.endsWith(']')) {
      const sec = t.toLowerCase()
      inScriptInfo = sec === '[script info]'
      inStyles = sec === '[v4+ styles]' || sec === '[v4 styles]'
      inEvents = sec === '[events]'
      continue
    }

    if (inScriptInfo) {
      const mx = t.match(/^PlayResX\s*:\s*(\d+)/i)
      const my = t.match(/^PlayResY\s*:\s*(\d+)/i)
      if (mx) playResX = Number(mx[1])
      if (my) playResY = Number(my[1])
    }

    if (inStyles) {
      if (t.toLowerCase().startsWith('format:')) {
        stylesFormatLine = t
        styleFormat = t.slice('format:'.length).split(',').map((x) => x.trim())
        continue
      }
      if (t.toLowerCase().startsWith('style:')) {
        const parts = t.slice('style:'.length).split(',').map((x) => x.trim())
        const name = parts[0]
        if (name) {
          stylesMap.set(name, parts)
          styleLinesRaw.push(t)
        }
        continue
      }
    }

    if (inEvents) {
      const lower = t.toLowerCase()
      if (lower.startsWith('comment:') || lower.startsWith('dialogue:')) {
        const isComment = lower.startsWith('comment:')
        const payload = t.replace(/^((comment|dialogue)\s*:)\s*/i, '')
        const ev = splitAssEventFields(payload)
        events.push({ raw: t, style: String(ev.style ?? '').trim(), effect: String(ev.effect ?? '').trim(), isComment })
      }
    }
  }

  if (!styleFormat || stylesMap.size === 0) return null

  const idx = (name: string) => styleFormat!.findIndex((x) => x.toLowerCase() === name.toLowerCase())
  const getNum = (arr: string[], key: string, fallback: number) => {
    const i = idx(key)
    if (i < 0) return fallback
    const v = Number(arr[i] ?? '')
    return Number.isFinite(v) ? v : fallback
  }
  const getStr = (arr: string[], key: string, fallback: string) => {
    const i = idx(key)
    if (i < 0) return fallback
    const v = String(arr[i] ?? '').trim()
    return v || fallback
  }
  const getBool = (arr: string[], key: string) => {
    const i = idx(key)
    if (i < 0) return false
    return String(arr[i] ?? '').trim() === '-1'
  }

  const styleNames = Array.from(stylesMap.keys())
  const pickBaseName = () => {
    const target = String(nameHint ?? '').trim()
    const noExt = target.replace(/\.ass$/i, '')
    if (noExt && stylesMap.has(noExt)) return noExt
    const candidate = styleNames.find((x) => x.toLowerCase() === noExt.toLowerCase())
    if (candidate) return candidate
    const baseCandidate = styleNames.find((x) => !x.toLowerCase().endsWith('-hi') && stylesMap.has(`${x}-Hi`))
    if (baseCandidate) return baseCandidate
    const baseLiteral = styleNames.find((x) => x.toLowerCase() === 'base')
    if (baseLiteral) return baseLiteral
    return styleNames[0]
  }

  const baseName = pickBaseName()
  const hiName = stylesMap.has(`${baseName}-Hi`) ? `${baseName}-Hi` : stylesMap.has('Hi') ? 'Hi' : baseName

  const baseArr = stylesMap.get(baseName)!
  const hiArr = stylesMap.get(hiName) ?? baseArr

  const base = {
    font: getStr(baseArr, 'Fontname', 'Arial'),
    size: getNum(baseArr, 'Fontsize', 56),
    bold: getBool(baseArr, 'Bold'),
    italic: getBool(baseArr, 'Italic'),
    primary: normalizeAssColor(getStr(baseArr, 'PrimaryColour', '&H00FFFFFF'), '&H00FFFFFF'),
    outlineColor: normalizeAssColor(getStr(baseArr, 'OutlineColour', '&H00101010'), '&H00101010'),
    backColor: normalizeAssColor(getStr(baseArr, 'BackColour', '&H64000000'), '&H64000000'),
    outline: getNum(baseArr, 'Outline', 2),
    shadow: getNum(baseArr, 'Shadow', 0),
  }
  const hi = {
    font: getStr(hiArr, 'Fontname', base.font),
    size: getNum(hiArr, 'Fontsize', base.size),
    bold: getBool(hiArr, 'Bold'),
    italic: getBool(hiArr, 'Italic'),
    primary: normalizeAssColor(getStr(hiArr, 'PrimaryColour', '&H00CCFF'), '&H00CCFF'),
    outlineColor: normalizeAssColor(getStr(hiArr, 'OutlineColour', '&H00101010'), '&H00101010'),
    backColor: normalizeAssColor(getStr(hiArr, 'BackColour', base.backColor), base.backColor),
    outline: getNum(hiArr, 'Outline', base.outline),
    shadow: getNum(hiArr, 'Shadow', base.shadow),
  }
  const layout = {
    alignment: getNum(baseArr, 'Alignment', 2),
    marginL: getNum(baseArr, 'MarginL', 60),
    marginR: getNum(baseArr, 'MarginR', 60),
    marginV: getNum(baseArr, 'MarginV', 60),
    playResX: playResX ?? 1920,
    playResY: playResY ?? 1080,
  }

  const hasTemplate = events.some((e) => e.isComment && e.effect.toLowerCase().startsWith('template'))
  const hasCode = events.some((e) => e.isComment && e.effect.toLowerCase().startsWith('code'))
  const templateStyleName = hasTemplate
    ? (() => {
        const counts = new Map<string, number>()
        for (const e of events) {
          if (!e.isComment) continue
          if (!e.effect.toLowerCase().startsWith('template')) continue
          const k = e.style
          if (!k) continue
          counts.set(k, (counts.get(k) ?? 0) + 1)
        }
        const sorted = Array.from(counts.entries()).sort((a, b) => b[1] - a[1])
        return sorted[0]?.[0] ?? baseName
      })()
    : undefined

  const preset: AssStylePreset = {
    id: `custom-${Date.now()}`,
    name: baseName,
    base,
    hi,
    layout,
    builtIn: false,
  }

  if (hasTemplate || hasCode) {
    preset.templateAssText = raw
    preset.templateStyleName = templateStyleName
    preset.effectText = `template-file (${templateStyleName ?? baseName})`
  } else {
    // Keep a lightweight hint for preview
    preset.effectText = stylesFormatLine ? 'styles-only' : undefined
  }

  return preset
}

const loadPresetFromFile = async () => {
  let fileHandle: any = null
  let file: File | null = null
  if ('showOpenFilePicker' in window) {
    try {
      const handles = await (window as any).showOpenFilePicker({
        multiple: false,
        types: [{ description: 'ASS files', accept: { 'text/plain': ['.ass'] } }],
      })
      fileHandle = handles[0]
      file = await fileHandle.getFile()
    } catch {}
  }
  if (!file) {
    const input = document.createElement('input')
    input.type = 'file'
    input.accept = '.ass,text/plain'
    input.onchange = async (e: any) => {
      file = e.target.files?.[0] ?? null
      if (file) await processFile(file)
    }
    input.click()
    return
  }
  if (file) await processFile(file)
}

const importTemplateFromFile = async () => {
  if (importingTemplate.value) return

  let fileHandle: any = null
  let file: File | null = null
  if ('showOpenFilePicker' in window) {
    try {
      const handles = await (window as any).showOpenFilePicker({
        multiple: false,
        types: [{ description: 'ASS files', accept: { 'text/plain': ['.ass'] } }],
      })
      fileHandle = handles[0]
      file = await fileHandle.getFile()
    } catch {}
  }
  if (!file) {
    const input = document.createElement('input')
    input.type = 'file'
    input.accept = '.ass,text/plain'
    input.onchange = async (e: any) => {
      file = e.target.files?.[0] ?? null
      if (!file) return
      await importTemplateFromPickedFile(file)
    }
    input.click()
    return
  }

  await importTemplateFromPickedFile(file)
}

const importTemplateFromPickedFile = async (file: File) => {
  try {
    importingTemplate.value = true
    const raw = await file.text()
    const content = String(raw ?? '').trim()
    if (!content) {
      alert('File rỗng.')
      return
    }

    const defaultName = file.name.replace(/\.ass$/i, '').trim() || 'Template'
    const name = (prompt('Template name', defaultName) ?? '').trim()
    if (!name) return

    await api.post('admin/ass-templates', {
      id: null,
      name,
      tags: null,
      content,
      isActive: true,
    })

    await refreshAssPresetsFromDb()
  } catch (e: any) {
    alert('ImportTemplate thất bại: ' + (e?.message ?? e))
  } finally {
    importingTemplate.value = false
  }
}

const processFile = async (file: File) => {
  try {
    const text = await file.text()
    const preset = parseAssFileToPreset(text, file.name)
    if (!preset) {
      alert('Không đọc được Styles trong file. (Cần [V4+ Styles] + Format/Style)')
      return
    }
    preset.name = preset.name || file.name.replace(/\.ass$/i, '')
    preset.builtIn = false
    assPresets.value = [...assPresets.value, preset]
    assPresetId.value = preset.id
    applyAssPresetById(preset.id)

    // Visible feedback for user
    if (preset.templateAssText && preset.templateStyleName) {
      loadStatus.value = `Loaded template: ${preset.name} (style: ${preset.templateStyleName})`
      lastLoadedAssText.value = String(preset.templateAssText)
    } else {
      loadStatus.value = `Loaded style: ${preset.name}`
      lastLoadedAssText.value = String(text ?? '')
    }
  } catch (e: any) {
    alert('Lỗi đọc file: ' + (e?.message ?? e))
  }
}

const normalizeAssColor = (v: string, fallback: string) => {
  const s = String(v ?? '').trim().toUpperCase()
  if (s.match(/^&H[0-9A-F]{8}$/)) return s
  const m = s.match(/^&H([0-9A-F]{2})([0-9A-F]{2})([0-9A-F]{2})([0-9A-F]{2})/)
  if (m) return `&H${m[1]}${m[2]}${m[3]}${m[4]}`
  return fallback
}

const splitAssEventFields = (line: string) => {
  // ASS event: Layer, Start, End, Style, Name, MarginL, MarginR, MarginV, Effect, Text
  // Text can contain commas, so we only split first 9 commas.
  const out: string[] = []
  let cur = ''
  let commas = 0
  const s = line
  for (let i = 0; i < s.length; i++) {
    const ch = s[i]
    if (ch === ',' && commas < 9) {
      out.push(cur)
      cur = ''
      commas++
      continue
    }
    cur += ch
  }
  out.push(cur)
  while (out.length < 10) out.push('')
  return {
    layer: out[0] ?? '',
    start: out[1] ?? '',
    end: out[2] ?? '',
    style: out[3] ?? '',
    name: out[4] ?? '',
    marginL: out[5] ?? '',
    marginR: out[6] ?? '',
    marginV: out[7] ?? '',
    effect: out[8] ?? '',
    text: out.slice(9).join(','),
  }
}

const removeExistingKaraokeFromTemplateAss = (ass: string) => {
  const raw = String(ass ?? '').replace(/\r\n/g, '\n')
  const ls = raw.split('\n')
  const out: string[] = []
  for (const line of ls) {
    const t = line.trim()
    if (!t) {
      out.push(line)
      continue
    }
    const lower = t.toLowerCase()
    if (lower.startsWith('comment:')) {
      const payload = t.replace(/^comment\s*:\s*/i, '')
      const ev = splitAssEventFields(payload)
      if (String(ev.effect ?? '').trim().toLowerCase() === 'karaoke') {
        continue
      }
    }
    out.push(line)
  }
  return out.join('\n')
}

const stripAssTemplaterExpr = (s: string) => {
  // Remove templater expressions like !retime(...)! or !math.random(...)!
  return String(s ?? '').replace(/![^!]*!/g, '')
}

const extractTemplateBodyFromTemplaterText = (s: string) => {
  const raw = stripAssTemplaterExpr(s).trim()
  const i0 = raw.indexOf('{')
  if (i0 < 0) return ''
  // keep everything from first '{' to end (includes shapes after '}' for particles)
  return raw.slice(i0).trim()
}

const guessTemplateKind = (effect: string, text: string): 'syl' | 'char' | 'line' => {
  const e = `${effect ?? ''}`.toLowerCase()
  const t = `${text ?? ''}`.toLowerCase()
  if (e.includes(' char') || t.includes('retime("char"') || t.includes('retime(\'char\'') || t.includes('template char')) return 'char'
  if (t.includes('retime("line"') || t.includes('retime(\'line\'') || t.includes('$lleft') || t.includes('$lmiddle') || t.includes('$ldur')) return 'line'
  return 'syl'
}

const parseAssTemplateToPresets = (text: string): AssStylePreset[] => {
  const raw = String(text ?? '').replace(/\r\n/g, '\n')
  const lines = raw.split('\n')

  let playResX: number | null = null
  let playResY: number | null = null
  let inScriptInfo = false
  let inStyles = false
  let inEvents = false
  let styleFormat: string[] | null = null
  let stylesFormatLine: string | null = null
  const styleLinesRaw: string[] = []

  // Parse [Script Info] and [V4+ Styles] for metadata and styles
  const stylesMap = new Map<string, any>()
  for (const line of lines) {
    const t = line.trim()
    if (!t) continue

    if (t.startsWith('[') && t.endsWith(']')) {
      inScriptInfo = t.toLowerCase() === '[script info]'
      inStyles = t.toLowerCase() === '[v4+ styles]'
      inEvents = t.toLowerCase() === '[events]'
      continue
    }

    if (inScriptInfo) {
      const mx = t.match(/^PlayResX\s*:\s*(\d+)/i)
      const my = t.match(/^PlayResY\s*:\s*(\d+)/i)
      if (mx) playResX = Number(mx[1])
      if (my) playResY = Number(my[1])
    }

    if (inStyles) {
      if (t.toLowerCase().startsWith('format:')) {
        stylesFormatLine = t
        styleFormat = t.slice('format:'.length).split(',').map((x) => x.trim())
        continue
      }
      if (t.toLowerCase().startsWith('style:')) {
        const parts = t.slice('style:'.length).split(',').map((x) => x.trim())
        const name = parts[0]
        if (name) {
          stylesMap.set(name, parts)
          styleLinesRaw.push(t)
        }
      }
    }
  }

  const buildPresetFromStyle = (styleName: string, templateAssText: string): AssStylePreset | null => {
    const styleDef = stylesMap.get(styleName)
    let baseStyle = defaultAssPresets[0]!.base
    let hiStyle = defaultAssPresets[0]!.hi
    let layout = defaultAssPresets[0]!.layout

    if (styleDef && styleFormat) {
      const idx = (name: string) => styleFormat!.findIndex((x) => x.toLowerCase() === name.toLowerCase())
      const get = (idx: number, fallback: string) => styleDef[idx]?.trim() || fallback
      const getNum = (idx: number, fallback: number) => {
        const v = Number(styleDef[idx] ?? '')
        return Number.isFinite(v) ? v : fallback
      }
      const getBool = (idx: number) => String(styleDef[idx] ?? '').trim() === '-1'

      const baseColorDefaults = defaultAssPresets[0]!.base
      const hiColorDefaults = defaultAssPresets[0]!.hi

      baseStyle = {
        font: get(idx('Fontname'), 'Arial'),
        size: getNum(idx('Fontsize'), 56),
        bold: getBool(idx('Bold')),
        italic: getBool(idx('Italic')),
        primary: baseColorDefaults.primary,
        outlineColor: baseColorDefaults.outlineColor,
        backColor: baseColorDefaults.backColor,
        outline: getNum(idx('Outline'), 2),
        shadow: getNum(idx('Shadow'), 0),
      }
      hiStyle = {
        ...baseStyle,
        primary: hiColorDefaults.primary,
        outlineColor: hiColorDefaults.outlineColor,
        backColor: hiColorDefaults.backColor,
      }
      layout = {
        alignment: getNum(idx('Alignment'), 2),
        marginL: getNum(idx('MarginL'), 60),
        marginR: getNum(idx('MarginR'), 60),
        marginV: getNum(idx('MarginV'), 60),
        playResX: playResX ?? 1920,
        playResY: playResY ?? 1080,
      }
    }

    return {
      id: `import-${Date.now()}-${Math.random().toString(16).slice(2)}`,
      name: `TEMPLATE: ${styleName}`,
      base: baseStyle,
      hi: hiStyle,
      layout,
      builtIn: false,
      effectText: `template-file (${styleName})`,
      templateAssText,
      templateStyleName: styleName,
    }
  }

  type KeptEvent = { i: number; line: string; style: string; effect: string }
  const keptEvents: KeptEvent[] = []
  for (let i = 0; i < lines.length; i++) {
    const trimmed = lines[i]?.trim() ?? ''
    if (!trimmed) continue
    const lower = trimmed.toLowerCase()

    // GOLDEN RULE: template file keeps only Comment code/template/karaoke. No Dialogue lines.
    if (lower.startsWith('dialogue:')) continue
    if (!lower.startsWith('comment:')) continue

    const payload = trimmed.replace(/^comment\s*:\s*/i, '')
    const ev = splitAssEventFields(payload)
    const styleName = String(ev.style ?? '').trim()
    const effect = String(ev.effect ?? '').trim()
    if (!styleName) continue

    const effectLower = effect.toLowerCase()
    const keep = effectLower.startsWith('code') || effectLower.startsWith('template') || effectLower === 'karaoke'
    if (!keep) continue
    keptEvents.push({ i, line: trimmed, style: styleName, effect })
  }

  const globalCode = keptEvents.filter((x) => x.effect.toLowerCase().startsWith('code'))
  const byStyle = new Map<string, { templates: KeptEvent[]; karaoke: KeptEvent[] }>()
  for (const ev of keptEvents) {
    const eff = ev.effect.toLowerCase()
    if (eff.startsWith('template')) {
      const cur = byStyle.get(ev.style) ?? { templates: [], karaoke: [] }
      cur.templates.push(ev)
      byStyle.set(ev.style, cur)
    } else if (eff === 'karaoke') {
      const cur = byStyle.get(ev.style) ?? { templates: [], karaoke: [] }
      cur.karaoke.push(ev)
      byStyle.set(ev.style, cur)
    }
  }

  const presets: AssStylePreset[] = []
  for (const [styleName, group] of byStyle.entries()) {
    if (!group.templates.length) continue

    const evLines = [...globalCode, ...group.templates, ...group.karaoke]
      .sort((a, b) => a.i - b.i)
      .map((x) => x.line)

    const header: string[] = []
    header.push('[Script Info]')
    header.push('Title: Karaoke Template Extracted')
    header.push('ScriptType: v4.00+')
    header.push('WrapStyle: 0')
    header.push('ScaledBorderAndShadow: yes')
    header.push(`PlayResX: ${playResX ?? 1280}`)
    header.push(`PlayResY: ${playResY ?? 720}`)
    header.push('')
    header.push('[V4+ Styles]')
    header.push(stylesFormatLine ?? 'Format: Name, Fontname, Fontsize, PrimaryColour, SecondaryColour, OutlineColour, BackColour, Bold, Italic, Underline, StrikeOut, ScaleX, ScaleY, Spacing, Angle, BorderStyle, Outline, Shadow, Alignment, MarginL, MarginR, MarginV, Encoding')
    header.push(...styleLinesRaw)
    header.push('')
    header.push('[Events]')
    header.push('Format: Layer, Start, End, Style, Name, MarginL, MarginR, MarginV, Effect, Text')

    const templateAssText = header.join('\n') + '\n' + evLines.join('\n') + '\n'
    const preset = buildPresetFromStyle(styleName, templateAssText)
    if (preset) presets.push(preset)
  }

  return presets
}
const saveCurrentToPreset = (id: string) => {
  const idx = assPresets.value.findIndex((x) => x.id === id)
  if (idx < 0) return
  const cur = getCurrentAssPresetSnapshot()
  assPresets.value[idx] = {
    ...assPresets.value[idx],
    base: cur.base,
    hi: cur.hi,
    layout: cur.layout,
  }
  saveAssPresets()
}

const addPresetFromCurrent = (name: string) => {
  const nm = (name ?? '').trim()
  if (!nm) return
  const cur = getCurrentAssPresetSnapshot()
  const id = `custom-${Date.now()}`
  const p: AssStylePreset = {
    id,
    name: nm,
    base: cur.base,
    hi: cur.hi,
    layout: cur.layout,
    builtIn: false,
  }
  assPresets.value = [...assPresets.value, p]
  assPresetId.value = id
  assPresetNewName.value = ''
  saveAssPresets()
}

const deletePreset = (id: string) => {
  const p = assPresets.value.find((x) => x.id === id)
  if (!p || p.builtIn) return
  assPresets.value = assPresets.value.filter((x) => x.id !== id)
  if (assPresetId.value === id) {
    assPresetId.value = assPresets.value[0]?.id ?? ''
    if (assPresetId.value) applyAssPresetById(assPresetId.value)
  }
  saveAssPresets()
}

onMounted(async () => {
  try {
    await refreshAssPresetsFromDb()
  } catch {
    assPresets.value = defaultAssPresets
    assPresetId.value = assPresets.value[0]?.id ?? ''
    if (assPresetId.value) applyAssPresetById(assPresetId.value)
  }
})

const onPickFile = (e: Event) => {
  const input = e.target as HTMLInputElement
  const f = input.files?.[0] ?? null
  file.value = f
  transcript.value = ''
  resultBlob.value = null
  error.value = ''
}

const generate = async () => {
  if (!file.value) return

  loading.value = true
  error.value = ''
  transcript.value = ''
  resultBlob.value = null

  try {
    const form = new FormData()
    form.append('file', file.value, file.value.name)
    form.append('format', outputFormat.value)
    if ((language.value ?? '').trim()) form.append('language', language.value.trim())

    const res = await axios.post('http://localhost:3099/api/transcribe', form, {
      headers: { 'Content-Type': 'multipart/form-data' },
      responseType: 'blob',
      timeout: 900000,
    })

    const blob = res.data as Blob

    resultBlob.value = blob

    if (outputFormat.value === 'json') {
      transcript.value = await blob.text()
    }
  } catch (e: any) {
    error.value = e?.message ?? 'Generate subtitle thất bại.'
  } finally {
    loading.value = false
  }
}

const download = () => {
  if (!resultBlob.value) return
  const ext = outputFormat.value
  const name = (file.value?.name ?? 'subtitle').replace(/\.[^/.]+$/, '')
  const url = URL.createObjectURL(resultBlob.value)

  const a = document.createElement('a')
  a.href = url
  a.download = `${name}.${ext}`
  a.click()

  setTimeout(() => URL.revokeObjectURL(url), 1000)
}

const reset = () => {
  file.value = null
  language.value = ''
  outputFormat.value = 'srt'
  transcript.value = ''
  resultBlob.value = null
  error.value = ''
}

const onPickTapMedia = (e: Event) => {
  const input = e.target as HTMLInputElement
  const f = input.files?.[0] ?? null
  tapMediaFile.value = f
  tapError.value = ''
  tapReset()
  if (!f) return
  tapIsVideo.value = (f.type ?? '').startsWith('video/')
  tapMediaUrl.value = URL.createObjectURL(f)

  if (!(tapAssName.value ?? '').trim()) {
    tapAssName.value = (f.name ?? '').replace(/\.[^/.]+$/, '').trim()
  }
}

const getMediaTime = () => {
  const el = tapMediaEl.value
  if (!el) return 0
  const t = Number(el.currentTime || 0)
  const off = Number(tapOffsetMs.value || 0) / 1000
  return Math.max(0, t + off)
}

const updateTapNow = () => {
  tapNow.value = getMediaTime()
  tapRaf = requestAnimationFrame(updateTapNow)
}

const tapReset = () => {
  tapRunning.value = false
  tapError.value = ''
  tapIndex.value = 0
  tapItems.value = []
  tapNow.value = 0
  tapPressTimes.value = []
  if (tapRaf != null) {
    cancelAnimationFrame(tapRaf)
    tapRaf = null
  }
}

const tapStart = async () => {
  tapError.value = ''
  if (!tapMediaEl.value) {
    tapError.value = 'Media element chưa sẵn sàng.'
    return
  }
  if (tapTargetCount.value === 0) {
    tapError.value = 'Bạn chưa nhập lyrics.'
    return
  }

  tapIndex.value = 0
  tapItems.value = []
  tapPressTimes.value = []
  tapRunning.value = true

  try {
    tapMediaEl.value.currentTime = 0
  } catch {}

  try {
    await tapMediaEl.value.play()
  } catch {
    tapError.value = 'Không thể auto play. Hãy bấm play trên player rồi bấm Start lại.'
    tapRunning.value = false
    return
  }

  if (tapRaf != null) cancelAnimationFrame(tapRaf)
  tapRaf = requestAnimationFrame(updateTapNow)
}

const tapStop = () => {
  if (tapRunning.value) {
    const metas = tapMode.value === 'line' ? tapLyricLines.value.map((x) => ({ w: x })) : tapWordMeta.value
    const press = tapPressTimes.value
    const i = tapIndex.value

    // If user has already started a word (pressed at least once) and stopped before pressing next,
    // close the current word using current media time.
    if (press.length > 0 && i > 0 && tapItems.value.length < i && (i - 1) < metas.length) {
      const start = press[press.length - 1]
      const now = getMediaTime()
      const minEnd = start + 0.12
      const end = Math.max(now, minEnd)
      if (end > start) {
        tapItems.value.push({ w: (metas as any)[i - 1].w, start, end })
      }
    }
  }
  tapRunning.value = false
  if (tapRaf != null) {
    cancelAnimationFrame(tapRaf)
    tapRaf = null
  }
}

const onTapHotkey = (e: KeyboardEvent) => {
  if (!tapRunning.value) return
  const key = (tapHotkey.value || 'A').toLowerCase()
  if (e.key?.toLowerCase() !== key) return

  e.preventDefault()
  e.stopPropagation()

  const targets = tapMode.value === 'line' ? tapLyricLines.value : tapWordMeta.value
  if (tapIndex.value >= targets.length) {
    tapStop()
    return
  }

  const t = getMediaTime()
  tapPressTimes.value.push(t)

  // Finalize previous word [pressTimes[n-2] -> pressTimes[n-1]] when we receive the next press.
  if (tapPressTimes.value.length >= 2) {
    const prevWordIndex = tapIndex.value - 1
    const start = tapPressTimes.value[tapPressTimes.value.length - 2]
    const end = tapPressTimes.value[tapPressTimes.value.length - 1]
    if (prevWordIndex >= 0 && prevWordIndex < targets.length && end > start) {
      const w = tapMode.value === 'line' ? String(targets[prevWordIndex] ?? '') : (targets as any)[prevWordIndex].w
      tapItems.value.push({ w, start, end })
    }
  }

  tapIndex.value++

  if (tapIndex.value >= targets.length) {
    tapStop()
  }
}

const pad2 = (n: number) => String(Math.floor(n)).padStart(2, '0')

const formatAssTime = (sec: number) => {
  const s = Math.max(0, sec)
  const h = Math.floor(s / 3600)
  const m = Math.floor((s % 3600) / 60)
  const ss = Math.floor(s % 60)
  const cs = Math.floor((s - Math.floor(s)) * 100)
  return `${h}:${pad2(m)}:${pad2(ss)}.${pad2(cs)}`
}

const buildBasicAssForSaving = () => {
  const items = tapItems.value
  const epsSec = 0.01

  if (tapMode.value === 'line') {
    const lines = tapLyricLines.value
    const lineObjs = items
      .map((it, idx) => {
        const text = lines[idx]
        if (!text) return null
        const startSec = Math.max(0, it.start)
        const endSec = Math.max(startSec, it.end)
        return { lineIndex: idx, startSec, endSec, text }
      })
      .filter(Boolean) as Array<{ lineIndex: number; startSec: number; endSec: number; text: string }>

    for (let i = 0; i < lineObjs.length - 1; i++) {
      const cur = lineObjs[i]
      const next = lineObjs[i + 1]
      const maxEnd = Math.max(cur.startSec, next.startSec - epsSec)
      if (cur.endSec > maxEnd) cur.endSec = maxEnd
    }

    const header = [
      '[Script Info]',
      'ScriptType: v4.00+',
      'PlayResX: 1920',
      'PlayResY: 1080',
      '',
      '[V4+ Styles]',
      'Format: Name, Fontname, Fontsize, PrimaryColour, SecondaryColour, OutlineColour, BackColour, Bold, Italic, Underline, StrikeOut, ScaleX, ScaleY, Spacing, Angle, BorderStyle, Outline, Shadow, Alignment, MarginL, MarginR, MarginV, Encoding',
      'Style: Default,Arial,56,&H00FFFFFF,&H00FFFFFF,&H00101010,&H64000000,0,0,0,0,100,100,0,0,1,3,0,2,60,60,60,1',
      '',
      '[Events]',
      'Format: Layer, Start, End, Style, Name, MarginL, MarginR, MarginV, Effect, Text',
    ].join('\n')

    const events = lineObjs
      .map((o) => {
        const endSec = o.endSec <= o.startSec ? o.startSec + epsSec : o.endSec
        const text = String(o.text ?? '').replace(/\{/g, '').replace(/\}/g, '')
        return `Dialogue: 0,${formatAssTime(o.startSec)},${formatAssTime(endSec)},Default,,0,0,0,,${text}`
      })
      .join('\n')

    return stripAssTransformTags(header + (events ? '\n' + events + '\n' : '\n'))
  }

  const metas = tapWordMeta.value
  const groups = new Map<number, TapItem[]>()

  for (let i = 0; i < items.length; i++) {
    const meta = metas[i]
    if (!meta) break
    const arr = groups.get(meta.line) ?? []
    arr.push(items[i])
    groups.set(meta.line, arr)
  }

  const lineObjs = Array.from(groups.entries())
    .sort((a, b) => a[0] - b[0])
    .map(([lineIndex, lineItems]) => {
      const valid = lineItems.filter((x) => (x.end ?? 0) > (x.start ?? 0))
      if (valid.length === 0) return null

      const merged = mergeAdjacentSameWords(valid)

      const startSec = Math.max(0, valid[0].start)
      const endSec = Math.max(startSec, valid[valid.length - 1].end)

      const karaokeText = merged
        .map((x) => {
          const s = Math.max(0, x.start)
          const e = Math.max(s, x.end)
          const durCs = Math.max(1, Math.round((e - s) * 100))
          const word = String(x.w ?? '').replace(/\{/g, '').replace(/\}/g, '')
          const count = Math.max(1, Math.floor(Number((x as any).count ?? 1)))
          const cnt = count > 1 ? `\\-${count}` : ''
          return `{\\k${durCs}${cnt}}${word}`
        })
        .join(' ')

      return { lineIndex, startSec, endSec, karaokeText }
    })
    .filter(Boolean) as Array<{ lineIndex: number; startSec: number; endSec: number; karaokeText: string }>

  for (let i = 0; i < lineObjs.length - 1; i++) {
    const cur = lineObjs[i]
    const next = lineObjs[i + 1]
    const maxEnd = Math.max(cur.startSec, next.startSec - epsSec)
    if (cur.endSec > maxEnd) cur.endSec = maxEnd
  }

  const header = [
    '[Script Info]',
    'ScriptType: v4.00+',
    'PlayResX: 1920',
    'PlayResY: 1080',
    '',
    '[V4+ Styles]',
    'Format: Name, Fontname, Fontsize, PrimaryColour, SecondaryColour, OutlineColour, BackColour, Bold, Italic, Underline, StrikeOut, ScaleX, ScaleY, Spacing, Angle, BorderStyle, Outline, Shadow, Alignment, MarginL, MarginR, MarginV, Encoding',
    'Style: Default,Arial,56,&H00FFFFFF,&H00FFFFFF,&H00101010,&H64000000,0,0,0,0,100,100,0,0,1,3,0,2,60,60,60,1',
    '',
    '[Events]',
    'Format: Layer, Start, End, Style, Name, MarginL, MarginR, MarginV, Effect, Text',
  ].join('\n')

  const events = lineObjs
    .map((o) => {
      const endSec = o.endSec <= o.startSec ? o.startSec + epsSec : o.endSec
      return `Dialogue: 0,${formatAssTime(o.startSec)},${formatAssTime(endSec)},Default,,0,0,0,,${o.karaokeText}`
    })
    .join('\n')

  return stripAssTransformTags(header + (events ? '\n' + events + '\n' : '\n'))
}

const buildAss = () => {
  const selectedPreset = getPresetById(assPresetId.value) ?? getCurrentAssPresetSnapshot()

  if (tapMode.value === 'line') {
    const lines = tapLyricLines.value
    const items = tapItems.value
    const epsSec = 0.01
    const leadSec = Math.max(0, Number(tapLeadMs.value || 0) / 1000)
    const preRollSec = Math.max(0, Number(tapPreRollMs.value || 0) / 1000)

    const lineObjs = items
      .map((it, idx) => {
        const text = lines[idx]
        if (!text) return null
        const startSec = Math.max(0, it.start - leadSec - preRollSec)
        const endSec = Math.max(startSec, it.end - leadSec)
        return { lineIndex: idx, startSec, endSec, text }
      })
      .filter(Boolean) as Array<{ lineIndex: number; startSec: number; endSec: number; text: string }>

    for (let i = 0; i < lineObjs.length - 1; i++) {
      const cur = lineObjs[i]
      const next = lineObjs[i + 1]
      const maxEnd = Math.max(cur.startSec, next.startSec - epsSec)
      if (cur.endSec > maxEnd) cur.endSec = maxEnd
    }

    if (selectedPreset?.templateAssText && String(selectedPreset.templateAssText).trim()) {
      const styleName = (selectedPreset.templateStyleName ?? '').trim() || 'Default'
      const overriddenTemplate = applyAssColorOverridesToTemplateAss(selectedPreset.templateAssText, styleName, {
        basePrimary: assBasePrimary.value,
        baseOutline: assBaseOutlineColor.value,
        hiPrimary: assHiPrimary.value,
        hiOutline: assHiOutlineColor.value,
      })
      const baseTemplate = removeExistingKaraokeFromTemplateAss(overriddenTemplate)

      const dialogueLines = lineObjs
        .map((o) => {
          const endSec = o.endSec <= o.startSec ? o.startSec + epsSec : o.endSec
          const text = String(o.text ?? '').replace(/\{/g, '').replace(/\}/g, '')
          return `Dialogue: 0,${formatAssTime(o.startSec)},${formatAssTime(endSec)},${styleName},,0,0,0,,${text}`
        })
        .join('\n')

      const glue = baseTemplate.endsWith('\n') ? '' : '\n'
      return baseTemplate + glue + (dialogueLines ? dialogueLines + '\n' : '')
    }

    const styleName = selectedPreset?.name?.trim() ? selectedPreset.name.trim() : 'Default'
    const b0 = assBaseBold.value ? -1 : 0
    const it0 = assBaseItalic.value ? -1 : 0
    const style = `Style: ${styleName},${assBaseFont.value},${assBaseFontSize.value},${assBasePrimary.value},${assBasePrimary.value},${assBaseOutlineColor.value},${assBaseBackColor.value},${b0},${it0},0,0,100,100,0,0,1,${assBaseOutline.value},${assBaseShadow.value},${assAlignment.value},${assMarginL.value},${assMarginR.value},${assMarginV.value},1`

    const header = [
      '[Script Info]',
      'ScriptType: v4.00+',
      `PlayResX: ${assPlayResX.value}`,
      `PlayResY: ${assPlayResY.value}`,
      '',
      '[V4+ Styles]',
      'Format: Name, Fontname, Fontsize, PrimaryColour, SecondaryColour, OutlineColour, BackColour, Bold, Italic, Underline, StrikeOut, ScaleX, ScaleY, Spacing, Angle, BorderStyle, Outline, Shadow, Alignment, MarginL, MarginR, MarginV, Encoding',
      style,
      '',
      '[Events]',
      'Format: Layer, Start, End, Style, Name, MarginL, MarginR, MarginV, Effect, Text',
    ].join('\n')

    const events = lineObjs
      .map((o) => {
        const endSec = o.endSec <= o.startSec ? o.startSec + epsSec : o.endSec
        const text = String(o.text ?? '').replace(/\{/g, '').replace(/\}/g, '')
        return `Dialogue: 0,${formatAssTime(o.startSec)},${formatAssTime(endSec)},${styleName},,0,0,0,,${text}`
      })
      .join('\n')

    return stripAssTransformTags(header + (events ? '\n' + events + '\n' : '\n'))
  }

  if (selectedPreset?.templateAssText && String(selectedPreset.templateAssText).trim()) {
    const styleName = (selectedPreset.templateStyleName ?? '').trim() || 'Default'
    const overriddenTemplate = applyAssColorOverridesToTemplateAss(selectedPreset.templateAssText, styleName, {
      basePrimary: assBasePrimary.value,
      baseOutline: assBaseOutlineColor.value,
      hiPrimary: assHiPrimary.value,
      hiOutline: assHiOutlineColor.value,
    })
    const baseTemplate = removeExistingKaraokeFromTemplateAss(overriddenTemplate)

    const metas = tapWordMeta.value
    const items = tapItems.value
    const groups = new Map<number, TapItem[]>()

    for (let i = 0; i < items.length; i++) {
      const meta = metas[i]
      if (!meta) break
      const arr = groups.get(meta.line) ?? []
      arr.push(items[i])
      groups.set(meta.line, arr)
    }

    const lineObjs = Array.from(groups.entries())
      .sort((a, b) => a[0] - b[0])
      .map(([lineIndex, lineItems]) => {
        const valid = lineItems.filter((x) => (x.end ?? 0) > (x.start ?? 0))
        if (valid.length === 0) return null

        const merged = mergeAdjacentSameWords(valid)

        const leadSec = Math.max(0, Number(tapLeadMs.value || 0) / 1000)
        const startSec = Math.max(0, valid[0].start - leadSec)
        const endSec = Math.max(startSec, valid[valid.length - 1].end - leadSec)

        let elapsedMs = 0
        const karaokeText = merged
          .map((x) => {
            const s = Math.max(0, x.start - leadSec)
            const e = Math.max(s, x.end - leadSec)
            const durSec = Math.max(0.01, e - s)
            const durCs = Math.max(1, Math.round(durSec * 100))
            const durMs = Math.max(10, Math.round(durSec * 1000))

            const t1 = Math.max(0, Math.round(elapsedMs))
            const t2 = Math.max(t1, Math.round(elapsedMs + durMs))
            elapsedMs += durMs

            const word = String(x.w ?? '').replace(/\{/g, '').replace(/\}/g, '')
            const count = Math.max(1, Math.floor(Number((x as any).count ?? 1)))
            const cnt = count > 1 ? `\\-${count}` : ''
            return `{\\k${durCs}${cnt}}${word}`
          })
          .join(' ')

        return { lineIndex, startSec, endSec, karaokeText }
      })
      .filter(Boolean) as Array<{ lineIndex: number; startSec: number; endSec: number; karaokeText: string }>

    const epsSec = 0.01
    for (let i = 0; i < lineObjs.length - 1; i++) {
      const cur = lineObjs[i]
      const next = lineObjs[i + 1]
      const maxEnd = Math.max(cur.startSec, next.startSec - epsSec)
      if (cur.endSec > maxEnd) cur.endSec = maxEnd
    }

    const karaokeLines = lineObjs
      .map((o) => {
        const endSec = o.endSec <= o.startSec ? o.startSec + epsSec : o.endSec
        return `Comment: 0,${formatAssTime(o.startSec)},${formatAssTime(endSec)},${styleName},,0,0,0,karaoke,${o.karaokeText}`
      })
      .join('\n')

    const glue = baseTemplate.endsWith('\n') ? '' : '\n'
    return baseTemplate + glue + (karaokeLines ? karaokeLines + '\n' : '')
  }

  const styleBaseName = selectedPreset?.name?.trim() ? selectedPreset.name.trim() : 'Base'
  const styleHiName = `${styleBaseName}-Hi`

  const b0 = assBaseBold.value ? -1 : 0
  const it0 = assBaseItalic.value ? -1 : 0
  const b1 = assHiBold.value ? -1 : 0
  const it1 = assHiItalic.value ? -1 : 0

  const styleBase = `Style: ${styleBaseName},${assBaseFont.value},${assBaseFontSize.value},${assBasePrimary.value},${assBasePrimary.value},${assBaseOutlineColor.value},${assBaseBackColor.value},${b0},${it0},0,0,100,100,0,0,1,${assBaseOutline.value},${assBaseShadow.value},${assAlignment.value},${assMarginL.value},${assMarginR.value},${assMarginV.value},1`
  const styleHi = `Style: ${styleHiName},${assHiFont.value},${assHiFontSize.value},${assHiPrimary.value},${assHiPrimary.value},${assHiOutlineColor.value},${assHiBackColor.value},${b1},${it1},0,0,100,100,0,0,1,${assHiOutline.value},${assHiShadow.value},${assAlignment.value},${assMarginL.value},${assMarginR.value},${assMarginV.value},1`

  const header = [
    '[Script Info]',
    'ScriptType: v4.00+',
    `PlayResX: ${assPlayResX.value}`,
    `PlayResY: ${assPlayResY.value}`,
    '',
    '[V4+ Styles]',
    'Format: Name, Fontname, Fontsize, PrimaryColour, SecondaryColour, OutlineColour, BackColour, Bold, Italic, Underline, StrikeOut, ScaleX, ScaleY, Spacing, Angle, BorderStyle, Outline, Shadow, Alignment, MarginL, MarginR, MarginV, Encoding',
    styleBase,
    styleHi,
    '',
    '[Events]',
    'Format: Layer, Start, End, Style, Name, MarginL, MarginR, MarginV, Effect, Text',
  ].join('\n')

  const metas = tapWordMeta.value
  const items = tapItems.value
  const groups = new Map<number, TapItem[]>()

  for (let i = 0; i < items.length; i++) {
    const meta = metas[i]
    if (!meta) break
    const arr = groups.get(meta.line) ?? []
    arr.push(items[i])
    groups.set(meta.line, arr)
  }

  const lineObjs = Array.from(groups.entries())
    .sort((a, b) => a[0] - b[0])
    .map(([lineIndex, lineItems]) => {
      const valid = lineItems.filter((x) => (x.end ?? 0) > (x.start ?? 0))
      if (valid.length === 0) return null

      const merged = mergeAdjacentSameWords(valid)

      const preRollCs = Math.max(0, Math.round((Number(tapPreRollMs.value || 0) / 1000) * 100))
      const leadSec = Math.max(0, Number(tapLeadMs.value || 0) / 1000)
      const startSec = Math.max(0, valid[0].start - leadSec - preRollCs / 100)
      const endSec = Math.max(startSec, valid[valid.length - 1].end - leadSec)
      const startMs = Math.round(startSec * 1000)

      const baseText = merged
        .map((x) => String(x.w ?? '').replace(/\r\n/g, ' ').replace(/\n/g, ' '))
        .join(' ')

      const hiWords = merged
        .map((x) => {
          const s = Math.max(0, x.start - leadSec)
          const e = Math.max(s, x.end - leadSec)

          const t1 = Math.max(0, Math.round(s * 1000) - startMs)
          const t2 = Math.max(t1, Math.round(e * 1000) - startMs)

          const durCs = Math.max(1, Math.round((e - s) * 100))
          const word = String(x.w ?? '').replace(/\{/g, '').replace(/\}/g, '')
          const count = Math.max(1, Math.floor(Number((x as any).count ?? 1)))
          const cnt = count > 1 ? `\\-${count}` : ''
          return `{\\kf${durCs}${cnt}}${word}`
        })
        .join(' ')

      const hiText = preRollCs > 0 ? `{\\alpha&HFF&\\kf${preRollCs}} ${hiWords}` : `{\\alpha&HFF&}${hiWords}`

      return {
        lineIndex,
        startSec,
        endSec,
        baseText,
        hiText,
      }
    })
    .filter(Boolean) as Array<{ lineIndex: number; startSec: number; endSec: number; baseText: string; hiText: string }>

  const epsSec = 0.01
  for (let i = 0; i < lineObjs.length - 1; i++) {
    const cur = lineObjs[i]
    const next = lineObjs[i + 1]
    const maxEnd = Math.max(cur.startSec, next.startSec - epsSec)
    if (cur.endSec > maxEnd) cur.endSec = maxEnd
  }

  const events = lineObjs
    .map((o) => {
      const endSec = o.endSec <= o.startSec ? o.startSec + epsSec : o.endSec
      const baseLine = `Dialogue: 0,${formatAssTime(o.startSec)},${formatAssTime(endSec)},${styleBaseName},,0,0,0,,${o.baseText}`
      const hiLine = `Dialogue: 1,${formatAssTime(o.startSec)},${formatAssTime(endSec)},${styleHiName},,0,0,0,,${o.hiText}`
      return `${baseLine}\n${hiLine}`
    })
  const body = events.join('\n')
  return stripAssTransformTags((header + (body ? '\n' + body : '')).replace(/\n{3,}/g, '\n\n').trimEnd() + '\n')
}

const applyAssColorOverridesToTemplateAss = (
  assText: string,
  styleName: string,
  colors: { basePrimary: string; baseOutline: string; hiPrimary: string; hiOutline: string },
) => {
  const raw = String(assText ?? '').replace(/\r\n/g, '\n')
  const ls = raw.split('\n')

  const baseStyleName = (styleName ?? '').trim() || 'Default'
  const hiStyleName = `${baseStyleName}-Hi`

  let inStyles = false
  let inEvents = false

  const normalizeAssColor = (c: string) => {
    const s = String(c ?? '').trim()
    return s || '&H00FFFFFF'
  }

  const colorDigitsFromMatch = (m: string) => {
    const mm = String(m ?? '').match(/&H([0-9A-Fa-f]{6,8})&/)
    const hex = (mm?.[1] ?? '').trim()
    return hex.length === 6 ? 6 : 8
  }

  const formatAssColorToDigits = (c: string, digits: 6 | 8) => {
    const s = normalizeAssColor(c)
    const mm = s.match(/&H([0-9A-Fa-f]+)\s*&?/) // accept '&Hxxxxxx' or '&Hxxxxxx&'
    let hex = (mm?.[1] ?? '').toUpperCase()
    if (!hex) hex = digits === 6 ? 'FFFFFF' : '00FFFFFF'

    // Colors in ASS are BBGGRR (6) or AABBGGRR (8). When downsizing, keep the last 6 digits.
    if (digits === 6) {
      hex = hex.padStart(6, '0')
      if (hex.length > 6) hex = hex.slice(-6)
      return `&H${hex}`
    }

    hex = hex.padStart(8, '0')
    if (hex.length > 8) hex = hex.slice(-8)
    return `&H${hex}`
  }

  const replaceOverrideColors = (text: string, primary: string, outline: string) => {
    let t = String(text ?? '')

    t = t.replace(/\\1c&H[0-9A-Fa-f]{6,8}&/g, (m) => {
      const digits = colorDigitsFromMatch(m) as 6 | 8
      const p = formatAssColorToDigits(primary, digits)
      return `\\1c${p}&`
    })

    t = t.replace(/\\3c&H[0-9A-Fa-f]{6,8}&/g, (m) => {
      const digits = colorDigitsFromMatch(m) as 6 | 8
      const o = formatAssColorToDigits(outline, digits)
      return `\\3c${o}&`
    })

    return t
  }

  const applyDefaultVsTransformOverrides = (s: string, basePrimary: string, baseOutline: string, hiPrimary: string, hiOutline: string) => {
    const input = String(s ?? '')
    const outParts: string[] = []
    let i = 0
    while (i < input.length) {
      const idx = input.indexOf('\\t(', i)
      if (idx < 0) {
        outParts.push(replaceOverrideColors(input.slice(i), basePrimary, baseOutline))
        break
      }

      if (idx > i) {
        outParts.push(replaceOverrideColors(input.slice(i, idx), basePrimary, baseOutline))
      }

      let j = idx + 3
      let depth = 1
      while (j < input.length && depth > 0) {
        const ch = input[j]
        if (ch === '(') depth++
        else if (ch === ')') depth--
        j++
      }

      const tBlock = input.slice(idx, j)
      outParts.push(replaceOverrideColors(tBlock, hiPrimary, hiOutline))
      i = j
    }

    return outParts.join('')
  }

  const rewriteEventText = (text: string) => {
    const src = String(text ?? '')
    const out: string[] = []
    let i = 0
    while (i < src.length) {
      const open = src.indexOf('{', i)
      if (open < 0) {
        out.push(src.slice(i))
        break
      }
      const close = src.indexOf('}', open + 1)
      if (close < 0) {
        out.push(src.slice(i))
        break
      }
      out.push(src.slice(i, open))
      const block = src.slice(open + 1, close)
      const rewritten = applyDefaultVsTransformOverrides(
        block,
        colors.basePrimary,
        colors.baseOutline,
        colors.hiPrimary,
        colors.hiOutline,
      )
      out.push('{' + rewritten + '}')
      i = close + 1
    }
    return out.join('')
  }

  const out: string[] = []
  for (const line of ls) {
    const trimmed = line.trim()

    if (/^\[v4\+\s*styles\]$/i.test(trimmed)) {
      inStyles = true
      inEvents = false
      out.push(line)
      continue
    }
    if (/^\[events\]$/i.test(trimmed)) {
      inStyles = false
      inEvents = true
      out.push(line)
      continue
    }
    if (/^\[.+\]$/.test(trimmed) && !/^\[v4\+\s*styles\]$/i.test(trimmed) && !/^\[events\]$/i.test(trimmed)) {
      inStyles = false
      inEvents = false
      out.push(line)
      continue
    }

    if (inStyles && /^style\s*:/i.test(trimmed)) {
      const rest = line.slice(line.indexOf(':') + 1)
      const parts = rest.split(',')
      const name = (parts[0] ?? '').trim()
      const isBase = name.localeCompare(baseStyleName, undefined, { sensitivity: 'accent' }) === 0
      const isHi = name.localeCompare(hiStyleName, undefined, { sensitivity: 'accent' }) === 0

      if (isBase || isHi) {
        // Format index: Name(0), Fontname(1), Fontsize(2), Primary(3), Secondary(4), Outline(5), Back(6)
        const primary = isHi ? colors.hiPrimary : colors.basePrimary
        const outline = isHi ? colors.hiOutline : colors.baseOutline
        if (parts.length > 3) parts[3] = normalizeAssColor(primary)
        if (parts.length > 5) parts[5] = normalizeAssColor(outline)
        out.push(`Style: ${parts.map((x) => String(x ?? '').trim()).join(',')}`)
        continue
      }

      out.push(line)
      continue
    }

    if (inEvents && (/^(comment|dialogue)\s*:/i.test(trimmed))) {
      const idx = line.indexOf(':')
      const prefix = idx >= 0 ? line.slice(0, idx + 1) : 'Dialogue:'
      const rest = idx >= 0 ? line.slice(idx + 1).trimStart() : line

      const fields: string[] = []
      let cur = ''
      let commaCount = 0
      for (let i = 0; i < rest.length; i++) {
        const ch = rest[i]
        if (ch === ',' && commaCount < 9) {
          fields.push(cur)
          cur = ''
          commaCount++
        } else {
          cur += ch
        }
      }
      fields.push(cur)
      const textFieldIndex = 9
      if (fields.length > textFieldIndex) {
        fields[textFieldIndex] = rewriteEventText(fields[textFieldIndex])
      }

      out.push(prefix + ' ' + fields.join(','))
      continue
    }

    out.push(line)
  }

  return out.join('\n')
}

const exportAss = () => {
  tapError.value = ''
  if (tapItems.value.length === 0) {
    tapError.value = 'Chưa có timing nào.'
    return
  }
  const content = buildAss()
  const blob = new Blob([content], { type: 'text/plain;charset=utf-8' })
  const url = URL.createObjectURL(blob)
  const a = document.createElement('a')
  a.href = url
  const base = sanitizeFileName((tapAssName.value ?? '').trim() || 'subtitle')
  a.download = `${base}.ass`
  a.click()
  setTimeout(() => URL.revokeObjectURL(url), 1000)
}

onMounted(() => {
  window.addEventListener('keydown', onTapHotkey, { capture: true })
})

onBeforeUnmount(() => {
  window.removeEventListener('keydown', onTapHotkey, { capture: true } as any)
  if (tapRaf != null) cancelAnimationFrame(tapRaf)
  if (tapMediaUrl.value) URL.revokeObjectURL(tapMediaUrl.value)
})
</script>

<style scoped>
@import url('https://fonts.googleapis.com/css2?family=Inter:wght@400;700;800&family=Roboto:wght@400;700;900&family=Open+Sans:wght@400;700;800&family=Lato:wght@400;700;900&family=Montserrat:wght@400;700;800&family=Poppins:wght@400;600;700;800&family=Nunito:wght@400;700;800&family=Be+Vietnam+Pro:wght@400;700;800&family=Oswald:wght@400;600;700&display=swap');

.page {
  padding: 16px;
}

.page-layout {
  display: grid;
  grid-template-columns: 1fr 620px;
  gap: 12px;
  height: 100%;
  align-items: start;
}

.page-sidebar {
  position: sticky;
    top: 12px;
    max-height: calc(100vh - 24px);
    overflow: auto;
    height: 100%;
}



.title {
  font-size: 18px;
  font-weight: 700;
}

.muted {
  opacity: 0.75;
  font-size: 13px;
}

.card {
  border: 1px solid rgba(255, 255, 255, 0.08);
  border-radius: 12px;
  padding: 14px;
  background: rgba(255, 255, 255, 0.02);
}

.row {
  display: grid;
  grid-template-columns: 1fr 220px 180px;
  gap: 10px;
}

.tap-row {
  grid-template-columns: 1fr 160px 160px 160px 160px;
}

.radio-row {
  display: flex;
  gap: 10px;
  flex-wrap: wrap;
  align-items: center;
  min-height: 40px;
}

.radio-item {
  display: inline-flex;
  gap: 6px;
  align-items: center;
  user-select: none;
}

.tap-style {
  margin-top: 10px;
}

.muted {
  opacity: 0.8;
}

.style-rows {
  display: grid;
  gap: 10px;
}

.style-row {
  padding: 10px;
  border: 1px solid rgba(255, 255, 255, 0.08);
  border-radius: 8px;
  background: rgba(255, 255, 255, 0.02);
}

.style-grid {
  margin-top: 6px;
  display: grid;
  grid-template-columns: repeat(4, minmax(0, 1fr));
  gap: 10px;
}

.preset-actions {
  display: flex;
  gap: 8px;
  flex-wrap: wrap;
}

.icon-toggles {
  display: flex;
  gap: 8px;
}

.icon-toggle {
  width: 34px;
  height: 34px;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  border: 1px solid rgba(255, 255, 255, 0.14);
  border-radius: 8px;
  background: rgba(255, 255, 255, 0.04);
  color: inherit;
  font-weight: 800;
  cursor: pointer;
}

.icon-toggle.on {
  border-color: rgba(255, 255, 255, 0.35);
  background: rgba(255, 255, 255, 0.12);
}

.icon-toggle:disabled {
  opacity: 0.6;
  cursor: not-allowed;
}

.color-field {
  display: grid;
  grid-template-columns: 42px 1fr;
  gap: 8px;
  align-items: center;
}

.color {
  width: 42px;
  height: 34px;
  padding: 0;
  border: none;
  background: transparent;
}

.preview-box {
  border: 1px solid rgba(255, 255, 255, 0.08);
  border-radius: 10px;
  padding: 10px;
  background: rgba(0, 0, 0, 0.15);
}

.karaoke-preview {
  margin-top: 8px;
  line-height: 1.15;
  word-break: break-word;
}

.karaoke-preview span {
  margin-right: 6px;
}

.modal-backdrop {
  position: fixed;
  inset: 0;
  background: rgba(0, 0, 0, 0.6);
  display: flex;
  align-items: center;
  justify-content: center;
  padding: 20px;
  z-index: 50;
}

.modal {
  width: min(980px, 100%);
  max-height: min(90vh, 900px);
  overflow: auto;
  border: 1px solid rgba(255, 255, 255, 0.12);
  border-radius: 12px;
  padding: 14px;
  background: rgba(20, 20, 20, 0.96);
}

.modal-head {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 12px;
  margin-bottom: 10px;
}

.import-list {
  margin-top: 12px;
  display: grid;
  gap: 10px;
}

.import-item {
  padding: 10px;
  border: 1px solid rgba(255, 255, 255, 0.08);
  border-radius: 12px;
  background: rgba(255, 255, 255, 0.02);
}

.ass-inspector-item {
  padding: 10px;
  border: 1px solid rgba(255, 255, 255, 0.08);
  border-radius: 12px;
  background: rgba(255, 255, 255, 0.02);
  margin-top: 8px;
}

.ass-kv {
  display: grid;
  grid-template-columns: 90px 1fr;
  gap: 10px;
  margin-top: 4px;
  align-items: center;
}

.color-row {
  margin-top: 6px;
  display: grid;
  grid-template-columns: 1fr 200px;
  gap: 10px;
  align-items: start;
}

.color-preview {
  padding: 10px;
}

.color-preview-line {
  margin-top: 8px;
  font-size: 18px;
  line-height: 1.1;
}

.swatch {
  display: inline-block;
  width: 12px;
  height: 12px;
  border-radius: 3px;
  margin-right: 6px;
  border: 1px solid rgba(255, 255, 255, 0.18);
  vertical-align: -2px;
}

.import-check {
  display: grid;
  grid-template-columns: 18px 1fr;
  gap: 10px;
  align-items: start;
}

.field .lbl {
  font-size: 12px;
  opacity: 0.8;
  margin-bottom: 6px;
}

.input {
  width: 100%;
}

.actions {
  margin-top: 12px;
  display: flex;
  gap: 10px;
  flex-wrap: wrap;
}

.btn {
  padding: 8px 12px;
}

.error {
  margin-top: 10px;
  color: #ff7272;
}

.preview {
  margin-top: 12px;
}

.textarea {
  width: 100%;
  resize: vertical;
}

.tap-body {
  margin-top: 10px;
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 12px;
}

.player {
  border: 1px solid rgba(255, 255, 255, 0.08);
  border-radius: 12px;
  padding: 10px;
  background: rgba(255, 255, 255, 0.02);
}

.video-stage {
  position: relative;
  width: 100%;
  max-width: 420px;
  margin: 0 auto;
  aspect-ratio: 9 / 16;
  border-radius: 12px;
  overflow: hidden;
  background: rgba(0, 0, 0, 0.35);
}

.video-stage .media {
  width: 100%;
  height: 100%;
  object-fit: cover;
  display: block;
}

.subtitle-stage {
  position: absolute;
  inset: 0;
  display: flex;
  pointer-events: none;
}

.subtitle-line {
  width: 100%;
  padding: 10px;
  text-align: center;
}

.media {
  width: 100%;
}

.tap-status {
  margin-top: 10px;
  display: grid;
  grid-template-columns: 140px 1fr;
  gap: 6px 10px;
}

.mono {
  font-family: ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, "Liberation Mono", "Courier New", monospace;
}

.tap-preview {
  margin-top: 10px;
}

.preview-words {
  margin-top: 6px;
  display: flex;
  flex-wrap: wrap;
  gap: 6px;
}

.w {
  padding: 3px 6px;
  border: 1px solid rgba(255, 255, 255, 0.08);
  border-radius: 999px;
  opacity: 0.75;
}

.w.active {
  opacity: 1;
  border-color: rgba(255, 255, 255, 0.28);
}

.w.done {
  opacity: 0.45;
}
</style>
