<template>
  <dialog ref="dialogEl" class="dialog" @cancel.prevent="close">
    <div class="modal modal-wide">
      <div class="modal-header">
        <div>
          <div class="modal-title">{{ titleText }}</div>
          <div class="muted">{{ subtitleText }}</div>
        </div>
        <button class="icon" type="button" :disabled="isRendering" @click="close">×</button>
      </div>

      <div class="modal-body">
        <div v-if="error" class="error">{{ error }}</div>

        <div class="layout">
          <div class="panel panel-content">
            <button class="section-head" type="button" :disabled="isRendering" @click="isImagesOpen = !isImagesOpen">
              <div>
                <div class="section-title">Danh sách ảnh</div>
                <div class="muted">Kéo thả để đổi thứ tự. Ảnh đầu tiên sẽ hiển thị trước trong video.</div>
              </div>
              <div class="section-head-right">
                <div class="panel-actions" @click.stop>
                  <button class="btn tiny secondary" type="button" :disabled="isRendering || orderedImages.length === 0" @click="selectPrev">
                    Trước
                  </button>
                  <button class="btn tiny secondary" type="button" :disabled="isRendering || orderedImages.length === 0" @click="selectNext">
                    Sau
                  </button>
                </div>
                <span class="chev" :class="{ open: isImagesOpen }">▾</span>
              </div>
            </button>

            <div v-show="isImagesOpen">
              <div v-if="loading" class="empty-state">Đang tải ảnh...</div>
              <div v-else-if="orderedImages.length === 0" class="empty-state">Không có ảnh để render.</div>
              <div v-else class="image-grid">
                <div
                  v-for="(url, idx) in orderedImages"
                  :key="url + idx"
                  class="image-card"
                  :class="{ active: idx === activeIndex, dragging: draggingImageUrl === url }"
                  :draggable="!isRendering"
                  @click="activeIndex = idx"
                  @dragstart="onImageDragStart(url)"
                  @dragover.prevent="onImageDragOver(url)"
                  @drop.prevent="onImageDrop(url)"
                  @dragend="onImageDragEnd"
                >
                  <div class="image-card-top">
                    <span class="order-badge">#{{ idx + 1 }}</span>
                    <span v-if="idx === activeIndex" class="active-badge">Active</span>
                    <button class="remove-image" type="button" :disabled="isRendering" @click.stop="removeImageAt(idx)">×</button>
                  </div>
                  
                  <img class="image-thumb" :src="url" alt="" />
                </div>
              </div>
            </div>

            <div class="audio-box">
              <button class="section-head" type="button" :disabled="isRendering" @click="isAudioOpen = !isAudioOpen">
                <div>
                  <div class="section-title">Âm thanh</div>
                  <div class="muted">Chọn music để trộn vào video preview + render.</div>
                </div>
                <div class="section-head-right">
                  <span class="chev" :class="{ open: isAudioOpen }">▾</span>
                </div>
              </button>

              <div v-show="isAudioOpen">
                <div class="audio-search">
                  <input v-model="musicQuery" class="input" placeholder="Tìm theo tên/author..." @keyup.enter="loadMusic" />

                  <select v-model="musicListMode" class="input" :disabled="isRendering" @change="resetMusicPageAndLoad">
                    <option value="all">Danh sách: Tất cả</option>
                    <option value="favorites">Danh sách: Yêu thích</option>
                  </select>

                  <select v-model.number="selectedTopicId" class="input" :disabled="isRendering || audioLoading" @change="resetMusicPageAndLoad">
                    <option :value="0">Topic: All</option>
                    <option v-for="t in musicTopics" :key="t.id" :value="t.id">{{ t.name }}</option>
                  </select>

                  <select v-model.number="selectedStyleId" class="input" :disabled="isRendering || audioLoading" @change="resetMusicPageAndLoad">
                    <option :value="0">Style: All</option>
                    <option v-for="s in musicStyles" :key="s.id" :value="s.id">{{ s.name }}</option>
                  </select>

                  <button class="btn secondary" type="button" :disabled="isRendering || audioLoading" @click="loadMusic">
                    {{ audioLoading ? 'Đang tải...' : 'Tải' }}
                  </button>
                  <button class="btn secondary" type="button" :disabled="isRendering" @click="clearAudio">
                    Bỏ chọn
                  </button>
                  <label class="audio-volume">
                  <div class="lbl">Volume</div>
                  <input v-model.number="audioVolume" class="input" type="number" min="0" max="2" step="0.1" />
                </label>
                </div>

                <div v-if="audioError" class="error">{{ audioError }}</div>

                

                <div v-if="audioResultsView.length" class="audio-results">
                  <button
                    v-for="it in audioResultsView"
                    :key="it.id"
                    type="button"
                    class="audio-item"
                    :class="{ active: selectedAudio?.id === it.id }"
                    :disabled="isRendering"
                    @click="selectAudio(it)"
                  >
                    <div class="audio-item-row">
                      <div class="audio-item-title">{{ it.name || 'Untitled' }}</div>
                      <div class="audio-item-actions">
                        <button class="btn tiny secondary" type="button" :disabled="isRendering" @click.stop="stopAudio">
                          Stop
                        </button>
                        <button class="btn tiny secondary" type="button" :disabled="isRendering" @click.stop="playTrack(it)">
                          Play
                        </button>
                        <button class="btn tiny secondary" type="button" :disabled="isRendering" @click.stop="toggleFavoriteTrack(it)">
                          {{ isFavoriteTrack(it) ? 'Bỏ thích' : 'Yêu thích' }}
                        </button>
                      </div>
                    </div>
                    <div class="audio-item-meta">{{ it.author ? (it.author + ' • ') : '' }}{{ formatDuration(it.duration) }}</div>
                  </button>
                </div>
                <div v-if="musicTotalView > 0 && musicListMode !== 'favorites'" class="audio-paging">
                  
                  <div class="muted">
                  <button class="btn tiny secondary" type="button" :disabled="isRendering || audioLoading || !canPrevMusic" @click="prevMusic">
                    Prev
                  </button>
                  Page {{ musicPage }} / {{ musicTotalPages }}
                  <button class="btn tiny secondary" type="button" :disabled="isRendering || audioLoading || !canNextMusic" @click="nextMusic">
                    Next
                  </button>
                  </div>
                  
                </div>
                <div v-else class="muted" style="margin-top: 4px">Chưa có audio.</div>
              </div>
            </div>

            <div class="text-box">
              <button class="section-head" type="button" :disabled="isRendering" @click="isTextOpen = !isTextOpen">
                <div>
                  <div class="section-title">Text overlay</div>
                  <div class="muted">Thêm text bằng FFmpeg (có animation). Timeline tính theo giây.</div>
                </div>
                <div class="section-head-right" @click.stop>
                  <div class="panel-actions" v-show="isTextOpen">
                    <button class="btn tiny secondary" type="button" :disabled="isRendering || orderedImages.length === 0" @click="addOverlayFromActive">
                      + Add from slide
                    </button>
                    <button class="btn tiny secondary" type="button" :disabled="isRendering" @click="addOverlay">
                      + Add
                    </button>
                    <button class="btn tiny secondary" type="button" :disabled="isRendering || isOverlayRendering || !textOverlays.length" @click="exportOverlayPng">
                      Xuất PNG
                    </button>
                  </div>
                  <span class="chev" :class="{ open: isTextOpen }">▾</span>
                </div>
              </button>

              <div v-show="isTextOpen">
                <div class="cta-box">
                  <label class="text-box-inline">
                    <input v-model="ctaEnabled" type="checkbox" :disabled="isRendering" />
                    <span class="lbl" style="margin: 0">CTA animation</span>
                  </label>

                  <div v-if="ctaEnabled" class="cta-grid">
                    <label>
                      <div class="lbl">Type</div>
                      <select v-model="ctaType" class="input" :disabled="isRendering">
                        <option value="text">Text</option>
                        <option value="image">Image</option>
                      </select>
                    </label>

                    <label v-if="ctaType === 'text'">
                      <div class="lbl">Text</div>
                      <input v-model="ctaText" class="input" :disabled="isRendering" placeholder="Follow me" />
                    </label>

                    <label v-else>
                      <div class="lbl">Image URL</div>
                      <input v-model="ctaImageUrl" class="input" :disabled="isRendering" placeholder="https://.../cta.png" />
                    </label>

                    <label>
                      <div class="lbl">Direction</div>
                      <select v-model="ctaDirection" class="input" :disabled="isRendering">
                        <option value="updown">Up-Down</option>
                        <option value="leftright">Left-Right</option>
                      </select>
                    </label>

                    <label>
                      <div class="lbl">Style</div>
                      <select v-model="ctaStyle" class="input" :disabled="isRendering">
                        <option value="sine">Sine</option>
                        <option value="bounce">Bounce</option>
                      </select>
                    </label>

                    <label class="small">
                      <div class="lbl">Amplitude (px)</div>
                      <input v-model.number="ctaAmplitudePx" class="input small" type="number" min="0" max="400" step="1" :disabled="isRendering" />
                    </label>

                    <label class="small">
                      <div class="lbl">Period (sec)</div>
                      <input v-model.number="ctaPeriodSec" class="input small" type="number" min="0.1" max="10" step="0.1" :disabled="isRendering" />
                    </label>

                    <label class="small">
                      <div class="lbl">X (px)</div>
                      <input v-model.number="ctaX" class="input small" type="number" step="1" :disabled="isRendering" />
                    </label>
                    <label class="small">
                      <div class="lbl">Y (px)</div>
                      <input v-model.number="ctaY" class="input small" type="number" step="1" :disabled="isRendering" />
                    </label>
                  </div>
                </div>

                <div v-if="textOverlays.length" class="text-list">
                  <div v-for="ov in textOverlays" :key="ov.id" class="text-item">
                    <div class="text-row">
                      <div class="textarea-wrapper">
                        <textarea v-model="ov.text" class="textarea overlay-textarea" placeholder="Text..." :disabled="isRendering" rows="2"></textarea>
                        <div class="icon-picker-dropdown">
                          <button type="button" class="icon-picker-btn" :disabled="isRendering" @click="toggleIconPicker(ov.id)">
                            <span class="icon-placeholder">🎨</span>
                          </button>
                          <div v-if="openIconPickerId === ov.id" class="icon-dropdown-panel">
                            <div class="icon-grid">
                              <button
                                v-for="icon in iconOptions"
                                :key="icon.key"
                                type="button"
                                class="icon-option"
                                @click="selectIcon(ov.id, icon.key, icon.color)"
                              >
                                <span class="icon-char" :style="{ color: icon.color }">{{ icon.char }}</span>
                                <span class="icon-name">{{ icon.name }}</span>
                              </button>
                            </div>
                          </div>
                          <div class="segment-text" style="display: flex;    gap: 8px;margin: 0 4px;">
                          <input v-model.number="ov.fromSec" class="small input tiny" type="number" min="0" step="0.1" :disabled="isRendering" placeholder="Từ (s)" />
                          <input v-model.number="ov.toSec" class="small input tiny" type="number" min="0" step="0.1" :disabled="isRendering" placeholder="Đến (s)" />
                          </div>
                          <div class="align-controls">
                            <div class="lbl">Align</div>
                            <div class="align-buttons">
                              <button type="button" class="align-btn" :class="{ active: (ov as any).textAlign === 'left' }" :disabled="isRendering" @click="(ov as any).textAlign = 'left'" title="Căn trái">
                                <svg viewBox="0 0 24 24" aria-hidden="true"><path fill="currentColor" d="M3 4h18v2H3V4zm0 4h12v2H3V8zm0 4h18v2H3v-2zm0 4h12v2H3v-2zm0 4h18v2H3v-2z"/></svg>
                              </button>
                              <button type="button" class="align-btn" :class="{ active: (ov as any).textAlign === 'center' }" :disabled="isRendering" @click="(ov as any).textAlign = 'center'" title="Căn giữa">
                                <svg viewBox="0 0 24 24" aria-hidden="true"><path fill="currentColor" d="M3 4h18v2H3V4zm3 4h12v2H6V8zm-3 4h18v2H3v-2zm3 4h12v2H6v-2zm-3 4h18v2H3v-2z"/></svg>
                              </button>
                              <button type="button" class="align-btn" :class="{ active: (ov as any).textAlign === 'right' }" :disabled="isRendering" @click="(ov as any).textAlign = 'right'" title="Căn phải">
                                <svg viewBox="0 0 24 24" aria-hidden="true"><path fill="currentColor" d="M3 4h18v2H3V4zm6 4h12v2H9V8zm-6 4h18v2H3v-2zm6 4h12v2H9v-2zm-6 4h18v2H3v-2z"/></svg>
                              </button>
                              <button type="button" class="align-btn" :class="{ active: (ov as any).textAlign === 'justify' }" :disabled="isRendering" @click="(ov as any).textAlign = 'justify'" title="Căn đều">
                                <svg viewBox="0 0 24 24" aria-hidden="true"><path fill="currentColor" d="M3 4h18v2H3V4zm0 4h18v2H3V8zm0 4h18v2H3v-2zm0 4h18v2H3v-2zm0 4h18v2H3v-2z"/></svg>
                              </button>
                            </div>
                          </div>
                        </div>
                    </div>
                      <button class="btn tiny secondary" type="button" :disabled="isRendering" @click="removeOverlay(ov.id)">
                        X
                      </button>
                    </div>
                    
                                       <div style  ="display: flex;gap: 10px;">
                    <div class="anchor-controls">
                        <div class="anchor-group">
                          <div class="lbl">Position</div>
                          <div class="anchor-buttons">
                            <button type="button" class="anchor-btn" :class="{ active: ov.anchorX === 'left' && ov.anchorY === 'top' }" @click="ov.anchorX = 'left'; ov.anchorY = 'top'" :disabled="isRendering">
                              <span class="icon">↖</span>
                            </button>
                            <button type="button" class="anchor-btn" :class="{ active: ov.anchorX === 'center' && ov.anchorY === 'top' }" @click="ov.anchorX = 'center'; ov.anchorY = 'top'" :disabled="isRendering">
                              <span class="icon">↑</span>
                            </button>
                            <button type="button" class="anchor-btn" :class="{ active: ov.anchorX === 'right' && ov.anchorY === 'top' }" @click="ov.anchorX = 'right'; ov.anchorY = 'top'" :disabled="isRendering">
                              <span class="icon">↗</span>
                            </button>
                            <button type="button" class="anchor-btn" :class="{ active: ov.anchorX === 'left' && ov.anchorY === 'center' }" @click="ov.anchorX = 'left'; ov.anchorY = 'center'" :disabled="isRendering">
                              <span class="icon">←</span>
                            </button>
                            <button type="button" class="anchor-btn" :class="{ active: ov.anchorX === 'center' && ov.anchorY === 'center' }" @click="ov.anchorX = 'center'; ov.anchorY = 'center'" :disabled="isRendering">
                              <span class="icon">●</span>
                            </button>
                            <button type="button" class="anchor-btn" :class="{ active: ov.anchorX === 'right' && ov.anchorY === 'center' }" @click="ov.anchorX = 'right'; ov.anchorY = 'center'" :disabled="isRendering">
                              <span class="icon">→</span>
                            </button>
                            <button type="button" class="anchor-btn" :class="{ active: ov.anchorX === 'left' && ov.anchorY === 'bottom' }" @click="ov.anchorX = 'left'; ov.anchorY = 'bottom'" :disabled="isRendering">
                              <span class="icon">↙</span>
                            </button>
                            <button type="button" class="anchor-btn" :class="{ active: ov.anchorX === 'center' && ov.anchorY === 'bottom' }" @click="ov.anchorX = 'center'; ov.anchorY = 'bottom'" :disabled="isRendering">
                              <span class="icon">↓</span>
                            </button>
                            <button type="button" class="anchor-btn" :class="{ active: ov.anchorX === 'right' && ov.anchorY === 'bottom' }" @click="ov.anchorX = 'right'; ov.anchorY = 'bottom'" :disabled="isRendering">
                              <span class="icon">↘</span>
                            </button>
                          </div>
                        </div>
                      </div>
                    <div class="text-grid">
                      
                      <label class="small">
                        <div class="lbl">X</div>
                        <input v-model.number="ov.offsetX" class="input small" type="number" step="1" :disabled="isRendering" />
                      </label>
                      <label class="small">
                        <div class="lbl">Y</div>
                        <input v-model.number="ov.offsetY" class="input small" type="number" step="1" :disabled="isRendering" />
                      </label>
                      <label>
                        <div class="lbl">Font</div>
                        <select v-model="ov.font" class="input" :disabled="isRendering">
                          <option value="default">Default</option>
                          <option value="segoeui">Segoe UI</option>
                          <option value="arial">Arial</option>
                          <option value="calibri">Calibri</option>
                          <option value="tahoma">Tahoma</option>
                          <option value="times">Times</option>
                          <option value="svn">SVN</option>
                          <option value="gf_poppins">GF: Poppins</option>
                          <option value="gf_montserrat">GF: Montserrat</option>
                          <option value="gf_bebas">GF: Bebas Neue</option>
                          <option value="gf_oswald">GF: Oswald</option>
                          <option value="gf_playfair">GF: Playfair Display</option>
                          <option value="gf_nunito">GF: Nunito</option>
                        </select>
                      </label>

                      <label class="small">
                        <div class="lbl">Size</div>
                        <input v-model.number="ov.fontSize" class="input small" type="number" min="8" max="200" step="1" :disabled="isRendering" />
                      </label>
                      <label class="small">
                        <div class="lbl">Rotate</div>
                        <input v-model.number="ov.rotate" class="input small" type="number" min="-180" max="180" step="1" :disabled="isRendering" />
                      </label>
                      <label class="small">
                        <div class="lbl">Color</div>
                        <input v-model="ov.fontColor" class="input color-input" type="color" :disabled="isRendering" />
                      </label>
                      <div class="style-controls">
                        <button type="button" class="style-btn" :class="{ active: ov.bold }" @click="ov.bold = !ov.bold" :disabled="isRendering">B</button>
                        <button type="button" class="style-btn" :class="{ active: ov.italic }" @click="ov.italic = !ov.italic" :disabled="isRendering">I</button>
                        <label class="text-box-inline">
                        <input v-model="ov.box" type="checkbox" :disabled="isRendering" />
                        <span class="lbl" style="margin: 0">Box</span>
                      </label>
                      </div>
                      
                      <label>
                        <div class="lbl">Box color</div>
                        <input v-model="ov.boxColor" class="input" placeholder="red@0.5" :disabled="!ov.box || isRendering" />
                      </label>
                      <label>
                        <div class="lbl">Box pad</div>
                        <input v-model.number="ov.boxBorderW" class="input" type="number" min="0" max="100" step="1" :disabled="!ov.box || isRendering" />
                      </label>
                      </div>
                    </div>
                  </div>
                </div>
                <div v-else class="muted">Chưa có text overlay.</div>
              </div>
            </div>
          </div>

          <div class="panel preview-panel">
            <div class="panel-head">
              <div>
                <div class="section-title">Preview layout Reel</div>
                <div class="muted">1 ảnh lớn phía trên, dãy thumbnail phía dưới, ảnh active được highlight.</div>
              </div>
              <label>
                <div class="lbl">Template</div>
                <select v-model.number="selectedTemplate" class="input">
                  <option :value="1">Template 1</option>
                  <option :value="2">Template 2 (fit height)</option>
                  <option :value="3">TemplateReupvideo (overlay PNG - WebM alpha)</option>
                  <option :value="4">Template overlay PNG (static)</option>
                </select>
              </label>
            </div>

            <div class="preview-stage">
              <div class="phone-frame">
                <div
                  ref="previewViewportEl"
                  class="preview-viewport"
                  :style="{ aspectRatio: renderSize.width + ' / ' + renderSize.height }"
                >
                  <Transition name="fade" mode="out-in">
                    <img v-if="activeImageUrl" :key="activeImageUrl" :src="activeImageUrl" alt="" />
                    <div v-else key="empty" class="empty-preview">Chưa có ảnh</div>
                  </Transition>

                  <div ref="thumbStripEl" class="thumb-strip" aria-label="Danh sách thumbnail">
                    <button
                      v-for="(url, idx) in orderedImages"
                      :key="url + '-thumb-' + idx"
                      class="thumb-item"
                      :class="{ active: idx === activeIndex }"
                      type="button"
                      @click="activeIndex = idx"
                    >
                      <img :src="url" alt="" />
                    </button>
                  </div>

                  <div class="overlay-layer" aria-hidden="true">
                    <template v-if="Number(selectedTemplate) === 3">
                      <img
                        v-for="it in previewOverlayImages"
                        :key="it.key"
                        class="overlay-img"
                        :src="it.url"
                        alt=""
                        :style="it.style"
                      />
                    </template>

                    <template v-else-if="Number(selectedTemplate) === 4">
                      <img
                        v-for="it in previewOverlayImagesStatic"
                        :key="it.key"
                        class="overlay-img"
                        :src="it.url"
                        alt=""
                        :style="it.style"
                      />
                    </template>

                    <div
                      v-for="ov in previewOverlays"
                      :key="ov.id"
                      class="overlay-item"
                      :style="ov.style"
                    >
                      {{ ov.text }}
                    </div>


                    <img
                      v-if="previewCta && previewCta.type === 'image'"
                      class="cta-img"
                      :src="previewCta.imageUrl"
                      alt=""
                      :style="previewCta.style"
                    />
                    <div
                      v-else-if="previewCta && previewCta.type === 'text'"
                      class="cta-text"
                      :style="previewCta.style"
                    >
                      {{ previewCta.text }}
                    </div>
                  </div>
                </div>
              </div>
            </div>

            <div class="config-grid">
              <label>
                <div class="lbl">Thời lượng mỗi ảnh (giây)</div>
                <input v-model.number="secondsPerSlide" class="input" type="number" min="1" max="10" step="1" />
              </label>
              <label>
                <div class="lbl">FPS</div>
                <input v-model.number="fps" class="input" type="number" min="12" max="60" step="1" />
              </label>
              <label>
                <div class="lbl">Kích thước render</div>
                <select v-model="selectedSize" class="input">
                  <option value="720x1280">720 x 1280</option>
                  <option value="1080x1920">1080 x 1920</option>
                </select>
              </label>

              <label style="grid-column: 1 / -1">
                <div class="lbl">Main video (để lấy duration)</div>
                <input class="input" type="file" accept="video/*" :disabled="isRendering" @change="onMainVideoPicked" />
              </label>

              <template v-if="Number(selectedTemplate) === 3">
                <label>
                  <div class="lbl">Kích thước ảnh overlay (px)</div>
                  <input v-model.number="template3BoxSize" class="input" type="number" min="40" max="600" step="1" />
                </label>
                <label>
                  <div class="lbl">Khoảng cách giữa ảnh (px)</div>
                  <input v-model.number="template3OffsetY" class="input" type="number" min="0" max="400" step="1" />
                </label>
                <label>
                  <div class="lbl">Lề phải (px)</div>
                  <input v-model.number="template3RightPad" class="input" type="number" min="0" max="400" step="1" />
                </label>
                <label>
                  <div class="lbl">Đi từ ngoài vào (px)</div>
                  <input v-model.number="template3StartXOffset" class="input" type="number" min="0" max="800" step="1" />
                </label>
                <label>
                  <div class="lbl">Base Y (px)</div>
                  <input v-model.number="template3BaseY" class="input" type="number" min="0" max="1600" step="1" />
                </label>
                <label>
                  <div class="lbl">Delay mỗi ảnh (giây)</div>
                  <input v-model.number="template3StaggerSec" class="input" type="number" min="0" max="3" step="0.01" />
                </label>
                <label>
                  <div class="lbl">Thời gian trượt vào (giây)</div>
                  <input v-model.number="template3MoveSec" class="input" type="number" min="0.05" max="5" step="0.01" />
                </label>
                <label>
                  <div class="lbl">Hướng animation</div>
                  <select v-model="template3AnimationDirection" class="input">
                    <option value="right-to-left">Từ phải sang trái (dính bên phải)</option>
                    <option value="left-to-right">Từ trái sang phải (dính bên trái)</option>
                  </select>
                </label>
              </template>

              <template v-else-if="Number(selectedTemplate) === 4">
                <label>
                  <div class="lbl">Kích thước ảnh overlay (px)</div>
                  <input v-model.number="template4BoxSize" class="input" type="number" min="40" max="600" step="1" />
                </label>
                <label>
                  <div class="lbl">Khoảng cách giữa ảnh (px)</div>
                  <input v-model.number="template4OffsetY" class="input" type="number" min="0" max="400" step="1" />
                </label>
                <label>
                  <div class="lbl">Lề phải (px)</div>
                  <input v-model.number="template4RightPad" class="input" type="number" min="0" max="400" step="1" />
                </label>
                <label>
                  <div class="lbl">Base Y (px)</div>
                  <input v-model.number="template4BaseY" class="input" type="number" min="0" max="1600" step="1" />
                </label>
              </template>
            </div>

            <div class="summary-row">
              <div class="summary-item"><span class="k">Ảnh:</span> <span class="v">{{ orderedImages.length }}</span></div>
              <div class="summary-item"><span class="k">Tổng thời lượng:</span> <span class="v">{{ totalDurationLabel }}</span></div>
              <div class="summary-item"><span class="k">Định dạng:</span> <span class="v">MP4</span></div>
            </div>

            <div v-if="renderError" class="error">{{ renderError }}</div>

            <div v-if="videoUrl" class="result-box">
              <video :src="videoUrl" controls playsinline preload="metadata" class="video-player" />
              <div class="result-actions">
                <a class="btn" :href="videoUrl" :download="downloadName">Tải video</a>
                <button class="btn secondary" type="button" @click="revokeVideo">Xoá preview</button>
              </div>
            </div>

            <div v-if="overlayWebmUrl" class="result-box">
              <video :src="overlayWebmUrl" controls playsinline preload="metadata" class="video-player" />
              <div class="result-actions">
                <a class="btn" :href="overlayWebmUrl" :download="overlayDownloadName">Tải overlay WebM</a>
                <button class="btn secondary" type="button" @click="revokeOverlayWebm">Xoá overlay</button>
              </div>
            </div>
          </div>
        </div>
      </div>

      <div class="overlay-render-root" aria-hidden="true">
        <iframe ref="overlayIframeEl" class="overlay-render-iframe" title="overlay-render" />
      </div>

      <div class="modal-footer">
        <button class="btn" type="button" :disabled="loading || isRendering || orderedImages.length === 0" @click="startRender">
          {{ isRendering ? 'Đang render...' : 'Render video' }}
        </button>
        <button class="btn secondary" type="button" :disabled="loading || isRendering || !canRenderOverlayWebm" @click="startRenderOverlayWebm">
          {{ isRendering ? 'Đang render...' : 'Render overlay WebM' }}
        </button>
        <button class="btn secondary" type="button" :disabled="isRendering" @click="close">Đóng</button>
      </div>
    </div>
  </dialog>
</template>

<script setup lang="ts">
import { computed, nextTick, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import api from '@/infrastructure/http/apiClient'
import html2canvas from 'html2canvas'

const props = withDefaults(
  defineProps<{
    modelValue: boolean
    productName?: string
    imageUrls: string[]
    loading?: boolean
    error?: string | null
    title?: string
  }>(),
  {
    productName: '',
    loading: false,
    error: null,
    title: 'RenderReels',
  },
)

const emit = defineEmits<{
  (e: 'update:modelValue', value: boolean): void
  (e: 'rendered', payload: { blob: Blob; url: string; images: string[] }): void
}>()

const orderedImages = ref<string[]>([])
const draggingImageUrl = ref('')
const activeIndex = ref(0)
const secondsPerSlide = ref(3)
const fps = ref(30)
const selectedSize = ref<'720x1280' | '1080x1920'>('720x1280')
const selectedTemplate = ref<1 | 2 | 3 | 4>(1)
const isRendering = ref(false)
const isOverlayRendering = ref(false)
const renderError = ref('')
const videoUrl = ref('')
const overlayWebmUrl = ref('')
const template3BoxSize = ref(124)
const template3OffsetY = ref(132)
const template3RightPad = ref(8)
const template3StartXOffset = ref(60)
const template3BaseY = ref(200)
const template3StaggerSec = ref(2)
const template3MoveSec = ref(0.55)
const template3AnimationDirection = ref<'right-to-left' | 'left-to-right'>('left-to-right')
const template3PreviewTimeSec = ref(0)
const template3PreviewTimer = ref<number | null>(null)

const template4BoxSize = ref(124)
const template4OffsetY = ref(132)
const template4RightPad = ref(8)
const template4BaseY = ref(200)

const mainVideoFile = ref<File | null>(null)
const mainVideoDurationSec = ref<number | null>(null)

type MusicTopicItem = { id: number; name: string }
type MusicStyleItem = { id: number; name: string }
type MusicItem = { id: string; name?: string; author?: string; duration: number; audioUrl: string }

type TextOverlayDraft = {
  id: string
  text: string
  fromSec: number
  toSec: number
  textAlign: 'left' | 'center' | 'right' | 'justify'
  animation: 'none' | 'float'
  animPeriodSec: number
  animAmplitudePx: number
  anchorX: 'left' | 'center' | 'right'
  anchorY: 'top' | 'center' | 'bottom'
  offsetX: number
  offsetY: number
  font: 'default' | 'arial' | 'segoeui' | 'calibri' | 'tahoma' | 'times' | 'svn' | 'gf_poppins' | 'gf_montserrat' | 'gf_bebas' | 'gf_oswald' | 'gf_playfair' | 'gf_nunito'
  fontSize: number
  fontColor: string
  bold: boolean
  italic: boolean
  rotate: number
  icon: string | null
  iconColor: string
  box: boolean
  boxColor: string
  boxBorderW: number
}

const overlayLoopDurationSec = ref(2)
const overlayLoopFps = ref(15)
const useAnimatedOverlay = ref(true)

const ctaEnabled = ref(false)
const ctaType = ref<'text' | 'image'>('text')
const ctaText = ref('Follow me')
const ctaImageUrl = ref('')
const ctaDirection = ref<'updown' | 'leftright'>('updown')
const ctaStyle = ref<'sine' | 'bounce'>('sine')
const ctaAmplitudePx = ref(18)
const ctaPeriodSec = ref(1.2)
const ctaX = ref(120)
const ctaY = ref(220)

const sleep = (ms: number) => new Promise<void>((r) => window.setTimeout(r, ms))

const buildOverlayWebmLoop = async (width: number, height: number) => {
  const dur = Math.max(0.5, Number(overlayLoopDurationSec.value) || 2)
  const fps = Math.max(6, Math.min(60, Number(overlayLoopFps.value) || 15))
  const totalFrames = Math.max(1, Math.round(dur * fps))

  const stage = await ensureOverlayIframe(width, height)
  const canvas = document.createElement('canvas')
  canvas.width = width
  canvas.height = height
  const ctx = canvas.getContext('2d', { alpha: true })
  if (!ctx) throw new Error('Canvas context not available')

  if (typeof MediaRecorder === 'undefined') throw new Error('MediaRecorder not supported')
  const mime = 'video/webm;codecs=vp9'
  if (!MediaRecorder.isTypeSupported(mime)) throw new Error(`Trình duyệt không hỗ trợ ${mime} (VP9)`) 

  const stream = canvas.captureStream(fps)
  const rec = new MediaRecorder(stream, { mimeType: mime, videoBitsPerSecond: 6_000_000 })
  const chunks: BlobPart[] = []
  rec.ondataavailable = (e) => {
    if (e.data && e.data.size > 0) chunks.push(e.data)
  }

  rec.start(250)
  try {
    for (let i = 0; i < totalFrames; i++) {
      const t = (i / fps) % dur
      stage.innerHTML = renderOverlayHtmlStatic(width, height, t)
      const frame = await html2canvas(stage, {
        backgroundColor: null,
        scale: 1,
        width,
        height,
        logging: false,
        useCORS: true,
        windowWidth: width,
        windowHeight: height,
      })
      ctx.clearRect(0, 0, width, height)
      ctx.drawImage(frame, 0, 0)
      await sleep(1000 / fps)
    }
  } finally {
    rec.stop()
  }

  return await new Promise<Blob>((resolve, reject) => {
    rec.onstop = () => resolve(new Blob(chunks, { type: mime }))
    rec.onerror = () => reject(new Error('MediaRecorder error'))
  })
}

const musicQuery = ref('')
const musicListMode = ref<'all' | 'favorites'>('all')
const selectedTopicId = ref(0)
const selectedStyleId = ref(162)
const musicTopics = ref<MusicTopicItem[]>([])
const musicStyles = ref<MusicStyleItem[]>([])

const favoriteTracks = ref<MusicItem[]>([])
const favoriteTracksKey = 'affiliate_platform_music_favorites'

const loadFavoriteTracks = () => {
  try {
    const raw = window.localStorage.getItem(favoriteTracksKey)
    if (!raw) {
      favoriteTracks.value = []
      return
    }
    const parsed = JSON.parse(raw)
    const list = Array.isArray(parsed) ? parsed : []
    favoriteTracks.value = list
      .map((x: any) => ({
        id: (x?.id ?? '').toString(),
        name: (x?.name ?? '').toString(),
        author: (x?.author ?? '').toString(),
        duration: Number(x?.duration) || 0,
        audioUrl: (x?.audioUrl ?? '').toString(),
      }))
      .filter((x: MusicItem) => !!x.id && !!x.audioUrl)
  } catch {
    favoriteTracks.value = []
  }
}

const saveFavoriteTracks = () => {
  try {
    window.localStorage.setItem(favoriteTracksKey, JSON.stringify(favoriteTracks.value))
  } catch {
  }
}

const isFavoriteTrack = (it: MusicItem) => {
  return favoriteTracks.value.some((x) => x.id === it.id)
}

const toggleFavoriteTrack = (it: MusicItem) => {
  const idx = favoriteTracks.value.findIndex((x) => x.id === it.id)
  if (idx >= 0) {
    favoriteTracks.value = favoriteTracks.value.filter((x) => x.id !== it.id)
  } else {
    favoriteTracks.value = [
      ...favoriteTracks.value,
      {
        id: it.id,
        name: it.name,
        author: it.author,
        duration: it.duration,
        audioUrl: it.audioUrl,
      },
    ]
  }
  saveFavoriteTracks()
}

watch(
  () => props.modelValue,
  (open) => {
    if (open) loadFavoriteTracks()
  },
  { immediate: true },
)

watch(
  () => musicListMode.value,
  (mode) => {
    if (mode === 'favorites') loadFavoriteTracks()
  },
)

const openIconPickerId = ref<string | null>(null)

const iconOptions = [
  // Face emojis
  { key: 'smile', char: '😊', name: 'Smile', color: '#FFD700' },
  { key: 'laugh', char: '😂', name: 'Laugh', color: '#FFD700' },
  { key: 'heart_eyes', char: '😍', name: 'Heart Eyes', color: '#FF1744' },
  { key: 'wink', char: '😉', name: 'Wink', color: '#FFD700' },
  { key: 'cool', char: '😎', name: 'Cool', color: '#2196F3' },
  { key: 'think', char: '🤔', name: 'Thinking', color: '#FF9800' },
  { key: 'sad', char: '😢', name: 'Sad', color: '#2196F3' },
  { key: 'angry', char: '😠', name: 'Angry', color: '#F44336' },
  { key: 'surprise', char: '😮', name: 'Surprise', color: '#FF9800' },
  { key: 'love', char: '😘', name: 'Love', color: '#FF1744' },
  { key: 'sleep', char: '😴', name: 'Sleep', color: '#607D8B' },
  { key: 'happy', char: '🥰', name: 'Happy', color: '#FF1744' },
  { key: 'grinning', char: '😀', name: 'Grinning', color: '#FFD700' },
  { key: 'grinning_big_eyes', char: '😃', name: 'Grinning Big Eyes', color: '#FFD700' },
  { key: 'grinning_smiling_eyes', char: '😄', name: 'Grinning Smiling Eyes', color: '#FFD700' },
  { key: 'beaming', char: '😁', name: 'Beaming', color: '#FFD700' },
  { key: 'grinning_squinting', char: '😆', name: 'Grinning Squinting', color: '#FFD700' },
  { key: 'grinning_sweat', char: '😅', name: 'Grinning Sweat', color: '#FFD700' },
  { key: 'rofl', char: '🤣', name: 'ROFL', color: '#FFD700' },
  { key: 'slight_smile', char: '🙂', name: 'Slight Smile', color: '#FFD700' },
  { key: 'upside_down', char: '🙃', name: 'Upside Down', color: '#FFD700' },
  { key: 'melting_face', char: '🫠', name: 'Melting Face', color: '#FFD700' },
  { key: 'smiling_eyes', char: '😊', name: 'Smiling Eyes', color: '#FFD700' },
  { key: 'halo', char: '😇', name: 'Halo', color: '#FFD700' },
  { key: 'hearts', char: '🥰', name: 'Hearts', color: '#FF1744' },
  { key: 'star_struck', char: '🤩', name: 'Star Struck', color: '#FFD700' },
  { key: 'kiss_face', char: '😗', name: 'Kiss', color: '#FF1744' },
  { key: 'smiling_face', char: '☺️', name: 'Smiling Face', color: '#FFD700' },
  { key: 'kiss_closed_eyes', char: '😚', name: 'Kiss Closed Eyes', color: '#FF1744' },
  { key: 'kiss_smiling_eyes', char: '😙', name: 'Kiss Smiling Eyes', color: '#FF1744' },
  { key: 'smile_tear', char: '🥲', name: 'Smile Tear', color: '#2196F3' },
  { key: 'savoring', char: '😋', name: 'Savoring Food', color: '#FFD700' },
  { key: 'tongue', char: '😛', name: 'Tongue', color: '#FFD700' },
  { key: 'winky_tongue', char: '😜', name: 'Winky Tongue', color: '#FFD700' },
  { key: 'zany', char: '🤪', name: 'Zany', color: '#FFD700' },
  { key: 'squinting_tongue', char: '😝', name: 'Squinting Tongue', color: '#FFD700' },
  { key: 'money_mouth', char: '🤑', name: 'Money Mouth', color: '#4CAF50' },
  { key: 'hugging', char: '🤗', name: 'Hugging', color: '#FFD700' },
  { key: 'hand_over_mouth', char: '🤭', name: 'Hand Over Mouth', color: '#FFD700' },
  { key: 'shocked_hand', char: '🫢', name: 'Shocked (Hand)', color: '#FFD700' },
  { key: 'peek', char: '🫣', name: 'Peeking', color: '#FFD700' },
  { key: 'shush', char: '🤫', name: 'Shush', color: '#FFD700' },
  { key: 'salute', char: '🫡', name: 'Salute', color: '#FFD700' },
  { key: 'zipper_mouth', char: '🤐', name: 'Zipper Mouth', color: '#FFD700' },
  { key: 'raised_eyebrow', char: '🤨', name: 'Raised Eyebrow', color: '#FFD700' },
  { key: 'neutral', char: '😐', name: 'Neutral', color: '#607D8B' },
  { key: 'expressionless', char: '😑', name: 'Expressionless', color: '#607D8B' },
  { key: 'no_mouth', char: '😶', name: 'No Mouth', color: '#607D8B' },
  { key: 'dotted_line', char: '🫥', name: 'Dotted Line Face', color: '#607D8B' },
  { key: 'face_in_clouds', char: '😶‍🌫️', name: 'Face in Clouds', color: '#607D8B' },
  { key: 'smirk', char: '😏', name: 'Smirk', color: '#FFD700' },
  { key: 'unamused', char: '😒', name: 'Unamused', color: '#607D8B' },
  { key: 'rolling_eyes', char: '🙄', name: 'Rolling Eyes', color: '#607D8B' },
  { key: 'grimace', char: '😬', name: 'Grimace', color: '#607D8B' },
  { key: 'exhale', char: '😮‍💨', name: 'Exhale', color: '#607D8B' },
  { key: 'lying', char: '🤥', name: 'Lying', color: '#607D8B' },
  { key: 'shaking', char: '🫨', name: 'Shaking', color: '#FF9800' },
  { key: 'head_shake', char: '🙂‍↔️', name: 'Head Shake', color: '#FF9800' },
  { key: 'head_nod', char: '🙂‍↕️', name: 'Head Nod', color: '#FF9800' },
  { key: 'relieved', char: '😌', name: 'Relieved', color: '#FFD700' },
  { key: 'pensive', char: '😔', name: 'Pensive', color: '#607D8B' },
  { key: 'sleepy', char: '😪', name: 'Sleepy', color: '#607D8B' },
  { key: 'drooling', char: '🤤', name: 'Drooling', color: '#607D8B' },
  { key: 'mask', char: '😷', name: 'Mask', color: '#607D8B' },
  { key: 'thermometer', char: '🤒', name: 'Thermometer', color: '#607D8B' },
  { key: 'head_bandage', char: '🤕', name: 'Head Bandage', color: '#607D8B' },
  { key: 'nauseated', char: '🤢', name: 'Nauseated', color: '#4CAF50' },
  { key: 'vomiting', char: '🤮', name: 'Vomiting', color: '#4CAF50' },
  { key: 'sneezing', char: '🤧', name: 'Sneezing', color: '#607D8B' },
  { key: 'hot', char: '🥵', name: 'Hot', color: '#F44336' },
  { key: 'cold', char: '🥶', name: 'Cold', color: '#2196F3' },
  { key: 'woozy', char: '🥴', name: 'Woozy', color: '#FF9800' },
  { key: 'dizzy', char: '😵', name: 'Dizzy', color: '#FF9800' },
  { key: 'dizzy_spiral', char: '😵‍💫', name: 'Dizzy Spiral', color: '#FF9800' },
  { key: 'mind_blown', char: '🤯', name: 'Mind Blown', color: '#FF9800' },
  { key: 'cowboy', char: '🤠', name: 'Cowboy', color: '#FFD700' },
  { key: 'partying', char: '🥳', name: 'Partying', color: '#FF9800' },
  { key: 'disguised', char: '🥸', name: 'Disguised', color: '#607D8B' },
  { key: 'sunglasses', char: '😎', name: 'Sunglasses', color: '#2196F3' },
  { key: 'nerd', char: '🤓', name: 'Nerd', color: '#2196F3' },
  { key: 'monocle', char: '🧐', name: 'Monocle', color: '#2196F3' },
  { key: 'confused', char: '😕', name: 'Confused', color: '#607D8B' },
  { key: 'diagonal_mouth', char: '🫤', name: 'Diagonal Mouth', color: '#607D8B' },
  { key: 'worried', char: '😟', name: 'Worried', color: '#607D8B' },
  { key: 'slightly_frown', char: '🙁', name: 'Slightly Frowning', color: '#607D8B' },
  { key: 'frown', char: '☹️', name: 'Frowning', color: '#607D8B' },
  { key: 'open_mouth', char: '😮', name: 'Open Mouth', color: '#FF9800' },
  { key: 'hushed', char: '😯', name: 'Hushed', color: '#FF9800' },
  { key: 'astonished', char: '😲', name: 'Astonished', color: '#FF9800' },
  { key: 'flushed', char: '😳', name: 'Flushed', color: '#FF9800' },
  { key: 'pleading', char: '🥺', name: 'Pleading', color: '#FF9800' },
  { key: 'holding_tears', char: '🥹', name: 'Holding Tears', color: '#2196F3' },
  { key: 'frown_open', char: '😦', name: 'Frown Open Mouth', color: '#607D8B' },
  { key: 'anguished', char: '😧', name: 'Anguished', color: '#607D8B' },
  { key: 'fearful', char: '😨', name: 'Fearful', color: '#607D8B' },
  { key: 'anxious_sweat', char: '😰', name: 'Anxious Sweat', color: '#607D8B' },
  { key: 'sad_relieved', char: '😥', name: 'Sad but Relieved', color: '#607D8B' },
  { key: 'cry', char: '😭', name: 'Crying', color: '#2196F3' },
  { key: 'scream', char: '😱', name: 'Screaming', color: '#FF9800' },
  { key: 'confounded', char: '😖', name: 'Confounded', color: '#607D8B' },
  { key: 'persevere', char: '😣', name: 'Persevering', color: '#607D8B' },
  { key: 'disappointed', char: '😞', name: 'Disappointed', color: '#607D8B' },
  { key: 'sweat', char: '😓', name: 'Downcast Sweat', color: '#607D8B' },
  { key: 'weary', char: '😩', name: 'Weary', color: '#607D8B' },
  { key: 'tired', char: '😫', name: 'Tired', color: '#607D8B' },
  { key: 'yawning', char: '🥱', name: 'Yawning', color: '#607D8B' },
  { key: 'triumph', char: '😤', name: 'Triumph', color: '#F44336' },
  { key: 'pouting', char: '😡', name: 'Pouting', color: '#F44336' },
  { key: 'rage', char: '🤬', name: 'Rage', color: '#F44336' },
  { key: 'smiling_horns', char: '😈', name: 'Smiling Horns', color: '#9C27B0' },
  { key: 'angry_horns', char: '👿', name: 'Angry Horns', color: '#9C27B0' },
  { key: 'skull', char: '💀', name: 'Skull', color: '#607D8B' },
  { key: 'skull_crossbones', char: '☠️', name: 'Skull Crossbones', color: '#607D8B' },
  
  // Other popular emojis
  { key: 'warning', char: '⚠️', name: 'Warning', color: '#FF9800' },
  { key: 'check', char: '✅', name: 'Check', color: '#4CAF50' },
  { key: 'cross', char: '❌', name: 'Cross', color: '#F44336' },
  { key: 'num_1', char: '1️⃣', name: '1', color: '#2196F3' },
  { key: 'num_2', char: '2️⃣', name: '2', color: '#2196F3' },
  { key: 'num_3', char: '3️⃣', name: '3', color: '#2196F3' },
  { key: 'num_4', char: '4️⃣', name: '4', color: '#2196F3' },
  { key: 'num_5', char: '5️⃣', name: '5', color: '#2196F3' },
  { key: 'num_6', char: '6️⃣', name: '6', color: '#2196F3' },
  { key: 'num_7', char: '7️⃣', name: '7', color: '#2196F3' },
  { key: 'num_8', char: '8️⃣', name: '8', color: '#2196F3' },
  { key: 'num_9', char: '9️⃣', name: '9', color: '#2196F3' },
  { key: 'fire', char: '🔥', name: 'Fire', color: '#FF6F00' },
  { key: 'star', char: '⭐', name: 'Star', color: '#FFD700' },
  { key: 'thumbs_up', char: '👍', name: 'Thumbs Up', color: '#4CAF50' },
  { key: 'clap', char: '👏', name: 'Clap', color: '#FFD700' },
  { key: 'party', char: '🎉', name: 'Party', color: '#FF9800' },
  { key: 'gift', char: '🎁', name: 'Gift', color: '#FF9800' },
  { key: 'heart', char: '❤️', name: 'Heart', color: '#FF1744' },
  { key: 'flower', char: '🌹', name: 'Rose', color: '#FF1744' },
  { key: 'money', char: '💰', name: 'Money', color: '#4CAF50' },
  { key: 'diamond', char: '💎', name: 'Diamond', color: '#2196F3' },
  { key: 'crown', char: '👑', name: 'Crown', color: '#9C27B0' },
  { key: 'rocket', char: '🚀', name: 'Rocket', color: '#FF6F00' },
]

const toggleIconPicker = (overlayId: string) => {
  if (openIconPickerId.value === overlayId) {
    openIconPickerId.value = null
  } else {
    openIconPickerId.value = overlayId
  }
}

// Handle click outside to close dropdown
const handleClickOutside = (event: MouseEvent) => {
  if (!openIconPickerId.value) return
  
  const target = event.target as Element
  const dropdown = target.closest('.icon-picker-dropdown')
  
  // If click is outside any dropdown, close all
  if (!dropdown) {
    openIconPickerId.value = null
  }
}

const selectIcon = (overlayId: string, iconKey: string, defaultColor: string) => {
  const overlay = textOverlays.value.find(x => x.id === overlayId)
  if (!overlay) return
  
  const icon = iconOptions.find(i => i.key === iconKey)
  if (!icon) return
  
  // Add icon to text content like Facebook chat
  overlay.text = (overlay.text || '') + icon.char
  overlay.iconColor = overlay.iconColor || defaultColor
  
  // Don't close picker - allow continuous selection
}
const audioLoading = ref(false)
const audioError = ref('')
const audioResults = ref<MusicItem[]>([])
const musicPage = ref(1)
const musicPageSize = ref(10)
const musicTotal = ref(0)
const selectedAudio = ref<MusicItem | null>(null)
const audioVolume = ref(0.6)
const previewAudioEl = ref<HTMLAudioElement | null>(null)
const previewTimer = ref<number | null>(null)
const ctaPreviewTimeSec = ref(0)
const ctaPreviewTimer = ref<number | null>(null)

const audioResultsView = computed(() => {
  if (musicListMode.value === 'favorites') return favoriteTracks.value
  return audioResults.value
})

const musicTotalView = computed(() => {
  if (musicListMode.value === 'favorites') return favoriteTracks.value.length
  return musicTotal.value
})

const textOverlays = ref<TextOverlayDraft[]>([])
let overlaySeq = 0

const isImagesOpen = ref(true)
const isAudioOpen = ref(true)
const isTextOpen = ref(true)

const dialogEl = ref<HTMLDialogElement | null>(null)

const googleFontsCssHref =
  'https://fonts.googleapis.com/css2?family=Poppins:wght@400;600;800&family=Montserrat:wght@400;600;800&family=Bebas+Neue&family=Oswald:wght@400;600;700&family=Playfair+Display:wght@400;600;700&family=Nunito:wght@400;700;800&display=swap'

const ensureGoogleFontsLoaded = () => {
  try {
    const id = 'reels-google-fonts'
    if (document.getElementById(id)) return
    const link = document.createElement('link')
    link.id = id
    link.rel = 'stylesheet'
    link.href = googleFontsCssHref
    document.head.appendChild(link)
  } catch {
  }
}

const makeOverlay = (partial: Partial<TextOverlayDraft> = {}): TextOverlayDraft => {
  const id = `ov_${Date.now()}_${++overlaySeq}`
  return {
    id,
    text: partial.text ?? '',
    fromSec: Number.isFinite(partial.fromSec as any) ? Number(partial.fromSec) : 0,
    toSec: Number.isFinite(partial.toSec as any) ? Number(partial.toSec) : 2,
    textAlign: (partial.textAlign as any) ?? 'center',
    animation: (partial.animation as any) ?? 'none',
    animPeriodSec: Number.isFinite(partial.animPeriodSec as any) ? Number(partial.animPeriodSec) : 1.2,
    animAmplitudePx: Number.isFinite(partial.animAmplitudePx as any) ? Number(partial.animAmplitudePx) : 18,
    anchorX: (partial.anchorX as any) ?? 'center',
    anchorY: (partial.anchorY as any) ?? 'top',
    offsetX: Number.isFinite(partial.offsetX as any) ? Number(partial.offsetX) : 0,
    offsetY: Number.isFinite(partial.offsetY as any) ? Number(partial.offsetY) : 160,
    font: (partial.font as any) ?? 'gf_oswald',
    fontSize: Number.isFinite(partial.fontSize as any) ? Number(partial.fontSize) : 90,
    fontColor: partial.fontColor ?? 'white',
    bold: partial.bold ?? false,
    italic: partial.italic ?? false,
    rotate: Number.isFinite(partial.rotate as any) ? Number(partial.rotate) : 0,
    icon: partial.icon ?? null,
    iconColor: partial.iconColor ?? '#FFD700',
    box: partial.box ?? true,
    boxColor: partial.boxColor ?? 'red@0.5',
    boxBorderW: Number.isFinite(partial.boxBorderW as any) ? Number(partial.boxBorderW) : 20,
  }
}

const addOverlay = () => {
  if (isRendering.value) return
  textOverlays.value = [...textOverlays.value, makeOverlay()]
}

const addOverlayFromActive = () => {
  if (isRendering.value) return
  textOverlays.value = [...textOverlays.value, makeOverlay()]
}

const removeOverlay = (id: string) => {
  textOverlays.value = textOverlays.value.filter((x) => x.id !== id)
}

const isOverlayActiveAt = (ov: Pick<TextOverlayDraft, 'fromSec' | 'toSec'>, t: number) => {
  const from = Math.max(0, Number(ov.fromSec) || 0)
  const to = Math.max(from, Number(ov.toSec) || 0)
  return t >= from && t <= to
}

const stopPreview = () => {
  if (previewTimer.value != null) {
    window.clearInterval(previewTimer.value)
    previewTimer.value = null
  }
}

const startPreview = () => {
  stopPreview()
  const ms = Math.max(1, Number(secondsPerSlide.value) || 3) * 1000
  previewTimer.value = window.setInterval(() => {
    selectNext()
  }, ms)
}

const stopCtaPreviewTimer = () => {
  if (ctaPreviewTimer.value != null) {
    window.clearInterval(ctaPreviewTimer.value)
    ctaPreviewTimer.value = null
  }
}

const startCtaPreviewTimer = () => {
  stopCtaPreviewTimer()
  if (!props.modelValue || !ctaEnabled.value) {
    ctaPreviewTimeSec.value = 0
    return
  }

  const startedAt = performance.now()
  ctaPreviewTimer.value = window.setInterval(() => {
    ctaPreviewTimeSec.value = (performance.now() - startedAt) / 1000
  }, 33)
}

const titleText = computed(() => props.title || 'RenderReels')
const subtitleText = computed(() => ((props.productName ?? '').trim() ? props.productName : 'Tạo slideshow video từ ảnh sản phẩm'))
const activeImageUrl = computed(() => orderedImages.value[activeIndex.value] ?? '')
const totalDurationSeconds = computed(() => orderedImages.value.length * Math.max(1, Number(secondsPerSlide.value) || 3))
const totalDurationLabel = computed(() => `${totalDurationSeconds.value}s`)
const previewTimeSeconds = computed(() => {
  const idx = Math.max(0, Number(activeIndex.value) || 0)
  const s = Math.max(1, Number(secondsPerSlide.value) || 3)
  return idx * s
})

const previewOverlayImagesStatic = computed(() => {
  const urls = overlayImageUrls.value
  const scaleX = previewScaleX.value
  const scaleY = previewScaleY.value
  const w = renderSize.value.width
  const rightPad = Math.max(0, Math.round(Number(template4RightPad.value) || 0))
  const baseY = Math.max(0, Math.round(Number(template4BaseY.value) || 0))
  const offsetY = Math.max(0, Math.round(Number(template4OffsetY.value) || 0))
  const boxW = Math.max(1, Math.round(Number(template4BoxSize.value) || 1))
  const boxH = boxW
  const baseX = Math.max(0, w - rightPad - boxW)

  return urls.map((url, i) => {
    const y = baseY + i * offsetY
    const style: any = {
      left: `${baseX * scaleX}px`,
      top: `${y * scaleY}px`,
      width: `${boxW * scaleX}px`,
      height: `${boxH * scaleY}px`,
      objectFit: 'contain',
      objectPosition: 'center',
      border: '2px solid white',
      borderRadius: '10px',
      zIndex: String(10 + i),
    }
    return { key: `${url}-${i}`, url, style }
  })
})

const overlayDownloadName = computed(() => {
  const name = (props.productName ?? 'overlay')
    .toLowerCase()
    .normalize('NFD')
    .replace(/[\u0300-\u036f]/g, '')
    .replace(/[^a-z0-9]+/g, '-')
    .replace(/(^-|-$)/g, '')
  return `${name || 'overlay'}.webm`
})

const overlayIframeEl = ref<HTMLIFrameElement | null>(null)

const escapeHtml = (s: string) =>
  (s ?? '')
    .toString()
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;')
    .replace(/'/g, '&#039;')

const styleObjToCss = (style: Record<string, any>) => {
  const parts: string[] = []
  for (const [k, v] of Object.entries(style)) {
    if (v === undefined || v === null) continue
    const key = k.replace(/[A-Z]/g, (m) => `-${m.toLowerCase()}`)
    parts.push(`${key}:${String(v)}`)
  }
  return parts.join(';')
}

const ensureOverlayIframe = async (width: number, height: number) => {
  await nextTick()
  const iframe = overlayIframeEl.value
  if (!iframe) throw new Error('Overlay iframe not ready')

  const doc = iframe.contentDocument
  if (!doc) throw new Error('Overlay iframe doc not ready')

  const existing = doc.getElementById('stage') as HTMLDivElement | null
  if (existing && existing.getAttribute('data-w') === String(width) && existing.getAttribute('data-h') === String(height)) {
    return existing
  }

  doc.open()
  doc.write(`<!doctype html><html><head><meta charset="utf-8" />
    <link rel="stylesheet" href="${googleFontsCssHref}" />
    <style>
      html,body{margin:0;padding:0;background:transparent;}
      #stage{position:relative;overflow:hidden;background:transparent;}
      .item{position:absolute;font-weight:400;line-height:1.15;white-space:pre;text-shadow:0 2px 14px rgba(0,0,0,.55);max-width:none; text-align:center;}
    </style>
  </head><body><div id="stage"></div></body></html>`)
  doc.close()

  const stage = doc.getElementById('stage') as HTMLDivElement | null
  if (!stage) throw new Error('Overlay stage not created')
  stage.style.width = `${width}px`
  stage.style.height = `${height}px`
  stage.setAttribute('data-w', String(width))
  stage.setAttribute('data-h', String(height))
  return stage
}

const renderOverlayHtmlStatic = (w: number, h: number, atTimeSec?: number) => {
  const list = textOverlays.value
    .map((x) => ({
      id: x.id,
      text: (x.text ?? '').toString().trim(),
      fromSec: Number(x.fromSec) || 0,
      toSec: Number(x.toSec) || 0,
      iconColor: x.iconColor || '#FFD700',
      textAlign: (x as any).textAlign,
      animation: (x as any).animation,
      animPeriodSec: Number((x as any).animPeriodSec) || 0,
      animAmplitudePx: Number((x as any).animAmplitudePx) || 0,
      anchorX: x.anchorX,
      anchorY: x.anchorY,
      offsetX: Number(x.offsetX) || 0,
      offsetY: Number(x.offsetY) || 0,
      font: x.font,
      fontSize: Number(x.fontSize) || 60,
      fontColor: (x.fontColor ?? 'white').toString(),
      bold: !!x.bold,
      italic: !!x.italic,
      rotate: Number(x.rotate) || 0,
      box: !!x.box,
      boxColor: (x.boxColor ?? 'red@0.5').toString(),
      boxBorderW: Number(x.boxBorderW) || 0,
    }))
    .filter((x) => x.text)
    .filter((x) => (typeof atTimeSec === 'number' ? isOverlayActiveAt(x, atTimeSec) : true))

  const items = list.map((x) => {
    let extraY = 0
    const anim = (x.animation ?? 'none').toString().toLowerCase()
    const t = typeof atTimeSec === 'number' ? atTimeSec : 0
    if (anim === 'float') {
      const period = Math.max(0.1, Number(x.animPeriodSec) || 1.2)
      const amp = Math.max(0, Number(x.animAmplitudePx) || 18)
      const localT = Math.max(0, t - (Number(x.fromSec) || 0))
      extraY = amp * Math.sin((2 * Math.PI * localT) / period)
    }

    const style = buildOverlayRenderStyle({ ...x, offsetY: Number(x.offsetY) + extraY }, w, h)
    // Icons are now part of the text content, no separate icon handling needed
    const content = escapeHtml(x.text)
    return `<div class="item" style="${styleObjToCss(style)}">${content}</div>`
  })

  if (ctaEnabled.value) {
    const t = typeof atTimeSec === 'number' ? atTimeSec : 0
    const amp = Math.max(0, Number(ctaAmplitudePx.value) || 0)
    const period = Math.max(0.1, Number(ctaPeriodSec.value) || 1)
    const dir = (ctaDirection.value ?? 'updown').toString()
    const st = (ctaStyle.value ?? 'sine').toString()

    const baseX = Number(ctaX.value) || 0
    const baseY = Number(ctaY.value) || 0

    let u = (t % period) / period
    let wave = 0
    if (st === 'bounce') {
      wave = 1 - 4 * Math.abs(u - 0.5)
    } else {
      wave = Math.sin(2 * Math.PI * u)
    }

    const dx = dir === 'leftright' ? amp * wave : 0
    const dy = dir === 'updown' ? amp * wave : 0

    const commonStyle: any = {
      position: 'absolute',
      left: `${baseX + dx}px`,
      top: `${baseY + dy}px`,
      zIndex: 20,
    }

    if (ctaType.value === 'image') {
      const src = (ctaImageUrl.value ?? '').toString().trim()
      if (src) {
        items.push(`<img class="cta-img" src="${escapeHtmlAttr(src)}" style="${styleObjToCss(commonStyle)}" />`)
      }
    } else {
      const txt = (ctaText.value ?? '').toString().trim()
      if (txt) {
        const style: any = {
          ...commonStyle,
          fontFamily: 'Oswald, Poppins, Montserrat, Arial, sans-serif',
          fontSize: '72px',
          fontWeight: '800',
          color: '#ffffff',
          textShadow: '0 6px 18px rgba(0,0,0,0.55)',
          whiteSpace: 'pre',
        }
        items.push(`<div class="cta-text" style="${styleObjToCss(style)}">${escapeHtml(txt)}</div>`)
      }
    }
  }

  return items.join('')
}

const computeCtaWave = (t: number) => {
  const amp = Math.max(0, Number(ctaAmplitudePx.value) || 0)
  const period = Math.max(0.1, Number(ctaPeriodSec.value) || 1)
  const dir = (ctaDirection.value ?? 'updown').toString()
  const st = (ctaStyle.value ?? 'sine').toString()
  const baseX = Number(ctaX.value) || 0
  const baseY = Number(ctaY.value) || 0

  const u = (t % period) / period
  const wave = st === 'bounce' ? 1 - 4 * Math.abs(u - 0.5) : Math.sin(2 * Math.PI * u)
  const dx = dir === 'leftright' ? amp * wave : 0
  const dy = dir === 'updown' ? amp * wave : 0
  return { x: baseX + dx, y: baseY + dy }
}

const previewCta = computed<null | { type: 'text' | 'image'; text?: string; imageUrl?: string; style: any }>(() => {
  if (!ctaEnabled.value) return null
  const t = Number(ctaPreviewTimeSec.value) || 0
  const p = computeCtaWave(t)
  const style: any = {
    position: 'absolute',
    left: `${p.x}px`,
    top: `${p.y}px`,
    zIndex: 20,
    pointerEvents: 'none',
  }

  if (ctaType.value === 'image') {
    const src = (ctaImageUrl.value ?? '').toString().trim()
    if (!src) return null
    return { type: 'image', imageUrl: src, style }
  }

  const txt = (ctaText.value ?? '').toString().trim()
  if (!txt) return null
  return {
    type: 'text',
    text: txt,
    style: {
      ...style,
      fontFamily: 'Oswald, Poppins, Montserrat, Arial, sans-serif',
      fontSize: '28px',
      fontWeight: '800',
      color: '#ffffff',
      textShadow: '0 6px 18px rgba(0,0,0,0.55)',
      whiteSpace: 'pre',
    },
  }
})

const escapeHtmlAttr = (s: string) =>
  (s ?? '')
    .toString()
    .replaceAll('&', '&amp;')
    .replaceAll('"', '&quot;')
    .replaceAll("'", '&#39;')
    .replaceAll('<', '&lt;')
    .replaceAll('>', '&gt;')

const buildOverlayPng = async (width: number, height: number): Promise<Blob> => {
  const stage = await ensureOverlayIframe(width, height)
  stage.innerHTML = renderOverlayHtmlStatic(width, height)

  const canvas = await html2canvas(stage, {
    backgroundColor: null,
    scale: 1,
    width,
    height,
    logging: false,
    useCORS: true,
    windowWidth: width,
    windowHeight: height,
  })

  return await new Promise<Blob>((resolve, reject) => {
    canvas.toBlob((b) => (b ? resolve(b) : reject(new Error('Failed to encode PNG'))), 'image/png')
  })
}

const canvasToPngBlob = async (canvas: HTMLCanvasElement): Promise<Blob> => {
  return await new Promise<Blob>((resolve, reject) => {
    canvas.toBlob((b) => (b ? resolve(b) : reject(new Error('Failed to encode PNG'))), 'image/png')
  })
}

const buildOverlayPngForOverlay = async (ov: TextOverlayDraft, width: number, height: number): Promise<Blob> => {
  const stage = await ensureOverlayIframe(width, height)

  const text = (ov.text ?? '').toString().trim()
  if (!text) {
    const empty = document.createElement('canvas')
    empty.width = Math.max(1, width)
    empty.height = Math.max(1, height)
    return await canvasToPngBlob(empty)
  }

  const data = {
    id: ov.id,
    text,
    fromSec: Number(ov.fromSec) || 0,
    toSec: Number(ov.toSec) || 0,
    iconColor: ov.iconColor || '#FFD700',
    textAlign: (ov as any).textAlign,
    animation: (ov as any).animation,
    animPeriodSec: Number((ov as any).animPeriodSec) || 0,
    animAmplitudePx: Number((ov as any).animAmplitudePx) || 0,
    anchorX: ov.anchorX,
    anchorY: ov.anchorY,
    offsetX: Number(ov.offsetX) || 0,
    offsetY: Number(ov.offsetY) || 0,
    font: ov.font,
    fontSize: Number(ov.fontSize) || 60,
    fontColor: (ov.fontColor ?? 'white').toString(),
    bold: !!ov.bold,
    italic: !!ov.italic,
    rotate: Number(ov.rotate) || 0,
    box: !!ov.box,
    boxColor: (ov.boxColor ?? 'red@0.5').toString(),
    boxBorderW: Number(ov.boxBorderW) || 0,
  }

  const style = buildOverlayRenderStyle(data, width, height)
  stage.innerHTML = `<div class="item" style="${styleObjToCss(style)}">${escapeHtml(text)}</div>`

  const canvas = await html2canvas(stage, {
    backgroundColor: null,
    scale: 1,
    width,
    height,
    logging: false,
    useCORS: true,
    windowWidth: width,
    windowHeight: height,
  })

  return await canvasToPngBlob(canvas)
}

const cropCanvasToAlphaBounds = (canvas: HTMLCanvasElement, pad: number) => {
  const w = canvas.width
  const h = canvas.height
  if (w <= 0 || h <= 0) return canvas

  const ctx = canvas.getContext('2d')
  if (!ctx) return canvas

  const img = ctx.getImageData(0, 0, w, h)
  const d = img.data

  let minX = w
  let minY = h
  let maxX = -1
  let maxY = -1

  for (let y = 0; y < h; y++) {
    const row = y * w * 4
    for (let x = 0; x < w; x++) {
      const a = d[row + x * 4 + 3]
      if (a > 0) {
        if (x < minX) minX = x
        if (y < minY) minY = y
        if (x > maxX) maxX = x
        if (y > maxY) maxY = y
      }
    }
  }

  if (maxX < 0 || maxY < 0) {
    const out = document.createElement('canvas')
    out.width = 1
    out.height = 1
    return out
  }

  const p = Math.max(0, Number(pad) || 0)
  const x0 = Math.max(0, minX - p)
  const y0 = Math.max(0, minY - p)
  const x1 = Math.min(w - 1, maxX + p)
  const y1 = Math.min(h - 1, maxY + p)
  const cw = Math.max(1, x1 - x0 + 1)
  const ch = Math.max(1, y1 - y0 + 1)

  const out = document.createElement('canvas')
  out.width = cw
  out.height = ch
  const outCtx = out.getContext('2d')
  if (!outCtx) return canvas
  outCtx.drawImage(canvas, x0, y0, cw, ch, 0, 0, cw, ch)
  return out
}

const downloadBlob = (blob: Blob, filename: string) => {
  const url = URL.createObjectURL(blob)
  const a = document.createElement('a')
  a.href = url
  a.download = filename
  document.body.appendChild(a)
  a.click()
  a.remove()
  setTimeout(() => URL.revokeObjectURL(url), 1000)
}

const exportOverlayPng = async () => {
  if (isRendering.value || isOverlayRendering.value) return
  const { width, height } = renderSize.value
  try {
    isOverlayRendering.value = true
    const stage = await ensureOverlayIframe(width, height)
    stage.innerHTML = renderOverlayHtmlStatic(width, height, previewTimeSeconds.value)

    const canvas = await html2canvas(stage, {
      backgroundColor: null,
      scale: 1,
      width,
      height,
      logging: false,
      useCORS: true,
      windowWidth: width,
      windowHeight: height,
    })

    const cropped = cropCanvasToAlphaBounds(canvas, 2)
    const blob = await canvasToPngBlob(cropped)
    const nameBase = (downloadName.value ?? 'render-reels').replace(/\.mp4$/i, '')
    downloadBlob(blob, `${nameBase}-overlay.png`)
  } finally {
    isOverlayRendering.value = false
  }
}
const downloadName = computed(() => {
  const name = (props.productName ?? 'render-reels')
    .toLowerCase()
    .normalize('NFD')
    .replace(/[\u0300-\u036f]/g, '')
    .replace(/[^a-z0-9]+/g, '-')
    .replace(/(^-|-$)/g, '')
  return `${name || 'render-reels'}.mp4`
})

const template2Height = ref(1280)

const renderSize = computed(() => {
  if (Number(selectedTemplate.value) === 2) {
    return { width: 720, height: Math.max(200, Math.round(template2Height.value || 1280)) }
  }
  const [widthText, heightText] = selectedSize.value.split('x')
  const width = Number(widthText) || 720
  const height = Number(heightText) || 1280
  return { width, height }
})

const canRenderOverlayWebm = computed(() => {
  const safeFps = Math.max(12, Math.min(60, Number(fps.value) || 30))
  const d = overlayDurationSeconds.value

  const hasImages = overlayImageUrls.value.length > 0
  const hasText = textOverlays.value.some((x) => (x.text ?? '').toString().trim())
  const hasCta = !!ctaEnabled.value

  return safeFps > 0 && d > 0 && !props.loading && (hasImages || hasText || hasCta)
})

const overlayDurationSeconds = computed(() => {
  const v = mainVideoDurationSec.value
  if (typeof v === 'number' && Number.isFinite(v) && v > 0) return v
  return totalDurationSeconds.value
})

const overlayImageUrls = computed(() => {
  return orderedImages.value
    .map((x) => (x ?? '').toString().trim())
    .filter((x) => !!x)
})

const effectiveTemplate3PreviewTime = computed(() => {
  if (Number(selectedTemplate.value) === 3) return template3PreviewTimeSec.value
  return previewTimeSeconds.value
})

const previewOverlayImages = computed(() => {
  void effectiveTemplate3PreviewTime.value
  const urls = overlayImageUrls.value
  const scaleX = previewScaleX.value
  const scaleY = previewScaleY.value
  const w = renderSize.value.width
  const h = renderSize.value.height
  const t = effectiveTemplate3PreviewTime.value

  const rightPad = Math.max(0, Math.round(Number(template3RightPad.value) || 0))
  const baseY = Math.max(0, Math.round(Number(template3BaseY.value) || 0))
  const offsetY = Math.max(0, Math.round(Number(template3OffsetY.value) || 0))
  const staggerSec = Math.max(0, Number(template3StaggerSec.value) || 0)
  const moveSec = Math.max(0.05, Number(template3MoveSec.value) || 0.55)
  const startXOffset = Math.max(0, Math.round(Number(template3StartXOffset.value) || 0))
  const boxW = Math.max(1, Math.round(Number(template3BoxSize.value) || 1))
  const boxH = boxW
  const dir = template3AnimationDirection.value
  const baseX = dir === 'right-to-left' ? Math.max(0, w - rightPad - boxW) : Math.max(0, rightPad)
  const startX = dir === 'right-to-left' ? w + startXOffset : -boxW - startXOffset

  return urls.map((url, i) => {
    const start = i * staggerSec
    const targetY = baseY + i * offsetY
    const y = targetY

    let x = startX
    if (t >= start) {
      const p = easeOutCubic((t - start) / moveSec)
      x = startX + (baseX - startX) * p
    }

    const style: any = {
      left: `${x * scaleX}px`,
      top: `${y * scaleY}px`,
      width: `${boxW * scaleX}px`,
      height: `${boxH * scaleY}px`,
      objectFit: 'contain',
      objectPosition: 'center',
      border: '2px solid white',
      borderRadius: '10px',
      zIndex: String(10 + i),
    }

    return { key: `${url}-${i}`, url, style }
  })
})

const imageSizeCache = new Map<string, { w: number; h: number }>()

const loadImageSize = async (url: string): Promise<{ w: number; h: number } | null> => {
  const u = (url ?? '').toString().trim()
  if (!u) return null
  const cached = imageSizeCache.get(u)
  if (cached) return cached
  return await new Promise((resolve) => {
    const img = new Image()
    img.onload = () => {
      const w = Number((img as any).naturalWidth) || Number(img.width) || 0
      const h = Number((img as any).naturalHeight) || Number(img.height) || 0
      const v = w > 0 && h > 0 ? { w, h } : null
      if (v) imageSizeCache.set(u, v)
      resolve(v)
    }
    img.onerror = () => resolve(null)
    img.crossOrigin = 'anonymous'
    img.src = u
  })
}

const computeTemplate2HeightFromImages = async () => {
  if (Number(selectedTemplate.value) !== 2) return
  const width = 720
  const outerPadding = Math.round(width * 0.035)
  const thumbGapX = Math.round(width * 0.018)
  const gapY = 8
  const visibleCount = Math.min(5, orderedImages.value.length)
  if (visibleCount <= 0) {
    template2Height.value = 1280
    return
  }
  const thumbSize = Math.round((width - outerPadding * 2 - thumbGapX * (visibleCount - 1)) / visibleCount)
  const thumbStripHeight = thumbSize
  const mainW = Math.max(1, width - outerPadding * 2)

  const sizes = await Promise.all(orderedImages.value.map((u) => loadImageSize(u)))
  let maxMainH = 1
  for (const s of sizes) {
    if (!s?.w || !s?.h) continue
    const scale = mainW / s.w
    const h = Math.max(1, Math.round(s.h * scale))
    if (h > maxMainH) maxMainH = h
  }

  template2Height.value = Math.max(200, outerPadding * 2 + maxMainH + gapY + thumbStripHeight)
}

watch(
  () => [selectedTemplate.value, orderedImages.value] as const,
  () => {
    void computeTemplate2HeightFromImages()
  },
  { immediate: true },
)

const previewScaleX = computed(() => {
  const renderW = Math.max(1, renderSize.value.width)
  const previewW = Math.max(0, Number(previewViewportWidth.value) || 0)
  if (previewW <= 0) return 1
  return previewW / renderW
})

const previewScaleY = computed(() => {
  const renderH = Math.max(1, renderSize.value.height)
  const previewH = Math.max(0, Number(previewViewportHeight.value) || 0)
  if (previewH <= 0) return 1
  return previewH / renderH
})

const previewViewportEl = ref<HTMLDivElement | null>(null)
const previewViewportWidth = ref(0)
const previewViewportHeight = ref(0)

const stopTemplate3PreviewTimer = () => {
  if (template3PreviewTimer.value != null) {
    window.clearInterval(template3PreviewTimer.value)
    template3PreviewTimer.value = null
  }
}

const startTemplate3PreviewTimer = () => {
  stopTemplate3PreviewTimer()
  if (!props.modelValue || Number(selectedTemplate.value) !== 3) {
    template3PreviewTimeSec.value = 0
    return
  }

  const total = Math.max(
    1,
    overlayImageUrls.value.length * Math.max(0, Number(template3StaggerSec.value) || 0) + Math.max(0.3, Number(template3MoveSec.value) || 0.55) + 0.8,
  )
  const startedAt = performance.now()
  template3PreviewTimer.value = window.setInterval(() => {
    const elapsed = (performance.now() - startedAt) / 1000
    template3PreviewTimeSec.value = elapsed % total
  }, 33)
}

onMounted(() => {
  ensureGoogleFontsLoaded()
  document.addEventListener('click', handleClickOutside)
  const el = previewViewportEl.value
  if (!el || typeof ResizeObserver === 'undefined') return
  const ro = new ResizeObserver((entries) => {
    const r = entries?.[0]?.contentRect
    const w = r?.width
    const h = r?.height
    if (typeof w === 'number' && w > 0) previewViewportWidth.value = w
    if (typeof h === 'number' && h > 0) previewViewportHeight.value = h
  })
  ro.observe(el)
  previewViewportWidth.value = el.clientWidth
  previewViewportHeight.value = el.clientHeight
  onBeforeUnmount(() => ro.disconnect())
})

watch(
  () => [props.modelValue, selectedTemplate.value, orderedImages.value.length, template3StaggerSec.value, template3MoveSec.value] as const,
  () => {
    startTemplate3PreviewTimer()
  },
  { immediate: true },
)

const previewOverlays = computed(() => {
  void previewTimeSeconds.value
  const scaleX = previewScaleX.value
  const scaleY = previewScaleY.value
  const w = renderSize.value.width
  const h = renderSize.value.height
  const t = previewTimeSeconds.value
  const list = textOverlays.value
    .map((x) => ({
      id: x.id,
      text: (x.text ?? '').toString().trim(),
      fromSec: Number(x.fromSec) || 0,
      toSec: Number(x.toSec) || 0,
      iconColor: x.iconColor || '#FFD700',
      textAlign: (x as any).textAlign,
      anchorX: x.anchorX,
      anchorY: x.anchorY,
      offsetX: Number(x.offsetX) || 0,
      offsetY: Number(x.offsetY) || 0,
      font: x.font,
      fontSize: Number(x.fontSize) || 60,
      fontColor: (x.fontColor ?? 'white').toString(),
      bold: !!x.bold,
      italic: !!x.italic,
      rotate: Number(x.rotate) || 0,
      box: !!x.box,
      boxColor: (x.boxColor ?? 'red@0.5').toString(),
      boxBorderW: Number(x.boxBorderW) || 0,
    }))
    .filter((x) => x.text)
    .filter((x) => isOverlayActiveAt(x, t))

  return list.map((x) => {
    const style = buildOverlayPreviewStyle(x, scaleX, scaleY, w, h)
    // Icons are now part of the text content, no separate icon handling needed
    return { id: x.id, text: x.text, style }
  })
})


const anchorPos = (anchorX: string, anchorY: string, offsetX: number, offsetY: number, w: number, h: number) => {
  const ax = (anchorX ?? 'center').toString().toLowerCase()
  const ay = (anchorY ?? 'top').toString().toLowerCase()
  const x = ax === 'left' ? 0 : ax === 'right' ? w : w / 2
  const y = ay === 'top' ? 0 : ay === 'bottom' ? h : h / 2
  return {
    x: x + (Number(offsetX) || 0),
    y: y + (Number(offsetY) || 0),
    cx: ax === 'center',
    cy: ay === 'center',
  }
}

const buildOverlayPreviewStyle = (
  ov: {
    anchorX: string
    anchorY: string
    offsetX: number
    offsetY: number
    font: string
    fontSize: number
    fontColor: string
    bold: boolean
    italic: boolean
    rotate: number
    box: boolean
    boxColor: string
    boxBorderW: number
    textAlign?: string
  },
  scaleX: number,
  scaleY: number,
  w: number,
  h: number,
) => {
  const p = anchorPos(ov.anchorX, ov.anchorY, ov.offsetX, ov.offsetY, w, h)
  const left = p.x * scaleX
  const top = p.y * scaleY
  const fontScale = Math.min(scaleX, scaleY)
  const fontSize = Math.max(8, ov.fontSize) * fontScale

  const align = (ov.textAlign ?? 'center').toString()

  const transforms: string[] = []
  // For preview, text-align only becomes visible when the element has width.
  // When user chooses non-center alignment, use full stage width so left/center/right/justify is noticeable.
  if (align === 'center') {
    if (p.cx) transforms.push('translateX(-50%)')
  }
  if (p.cy) transforms.push('translateY(-50%)')
  if (ov.rotate) transforms.push(`rotate(${ov.rotate}deg)`)

  const style: any = {
    left: align === 'center' ? `${left}px` : `0px`,
    top: `${top}px`,
    fontSize: `${fontSize}px`,
    color: ffmpegColorToCss(ov.fontColor || 'white'),
    opacity: '1',
    transform: transforms.join(' '),
    fontFamily: fontKeyToCssFamily(ov.font),
    whiteSpace: 'pre',
    textAlign: align,
    fontWeight: ov.bold ? 'bold' : 'normal',
    fontStyle: ov.italic ? 'italic' : 'normal',
  }

  if (align !== 'center') {
    style.width = `${Math.max(1, w * scaleX)}px`
  }

  if (ov.box)
  {
    const pad = Math.max(0, ov.boxBorderW) * fontScale
    style.background = ffmpegColorToCss(ov.boxColor || 'red@0.5')
    style.padding = `${pad}px ${pad}px`
    style.borderRadius = `${Math.max(6, 12 * fontScale)}px`
  }

  return style
}

const buildOverlayRenderStyle = (
  ov: {
    anchorX: string
    anchorY: string
    offsetX: number
    offsetY: number
    font: string
    fontSize: number
    fontColor: string
    bold: boolean
    italic: boolean
    rotate: number
    box: boolean
    boxColor: string
    boxBorderW: number
    textAlign?: string
  },
  w: number,
  h: number,
) => {
  const p = anchorPos(ov.anchorX, ov.anchorY, ov.offsetX, ov.offsetY, w, h)
  const transforms: string[] = []
  if (p.cx) transforms.push('translateX(-50%)')
  if (p.cy) transforms.push('translateY(-50%)')
  if (ov.rotate) transforms.push(`rotate(${ov.rotate}deg)`)

  const style: any = {
    left: `${p.x}px`,
    top: `${p.y}px`,
    fontSize: `${Math.max(8, ov.fontSize)}px`,
    color: ffmpegColorToCss(ov.fontColor || 'white'),
    opacity: '1',
    transform: transforms.join(' '),
    fontFamily: fontKeyToCssFamily(ov.font),
    whiteSpace: 'pre',
    textAlign: (ov.textAlign ?? 'center').toString(),
    fontWeight: ov.bold ? 'bold' : 'normal',
    fontStyle: ov.italic ? 'italic' : 'normal',
  }

  if (ov.box) {
    const pad = Math.max(0, ov.boxBorderW)
    style.background = ffmpegColorToCss(ov.boxColor || 'red@0.5')
    style.padding = `${pad}px ${pad}px`
    style.borderRadius = `12px`
  }

  return style
}

const fontKeyToCssFamily = (k: string) => {
  const key = (k ?? '').toString().trim().toLowerCase()
  if (key === 'arial') return 'Arial, sans-serif'
  if (key === 'segoeui' || key === 'segoe ui') return 'Segoe UI, sans-serif'
  if (key === 'calibri') return 'Calibri, sans-serif'
  if (key === 'tahoma') return 'Tahoma, sans-serif'
  if (key === 'times' || key === 'timesnewroman' || key === 'times new roman') return 'Times New Roman, serif'
  if (key === 'svn') return 'SVN-Headliner No. 45, sans-serif'
  if (key.startsWith('gf_')) {
    const name = key.replace('gf_', '').replace(/_/g, ' ').replace(/\b\w/g, l => l.toUpperCase())
    return name
  }
  return 'sans-serif'
}

const ffmpegColorToCss = (raw: string) => {
  const s = (raw ?? '').toString().trim()
  if (!s) return 'white'

  // Convert ffmpeg style: "red@0.5" into rgba()
  const m = s.match(/^([a-zA-Z]+|#[0-9a-fA-F]{6}|#[0-9a-fA-F]{3})\s*@\s*(\d+(?:\.\d+)?)$/)
  if (!m) return s

  const base = m[1]
  const a = Math.max(0, Math.min(1, Number(m[2])))
  const rgb = cssColorToRgb(base)
  if (!rgb) return base
  return `rgba(${rgb.r}, ${rgb.g}, ${rgb.b}, ${a})`
}

const cssColorToRgb = (base: string): { r: number; g: number; b: number } | null => {
  const c = base.trim().toLowerCase()

  const named: Record<string, { r: number; g: number; b: number }> = {
    black: { r: 0, g: 0, b: 0 },
    white: { r: 255, g: 255, b: 255 },
    red: { r: 255, g: 0, b: 0 },
    green: { r: 0, g: 128, b: 0 },
    blue: { r: 0, g: 0, b: 255 },
    yellow: { r: 255, g: 255, b: 0 },
    orange: { r: 255, g: 165, b: 0 },
    gray: { r: 128, g: 128, b: 128 },
    grey: { r: 128, g: 128, b: 128 },
    cyan: { r: 0, g: 255, b: 255 },
    magenta: { r: 255, g: 0, b: 255 },
  }
  if (named[c]) return named[c]

  if (/^#[0-9a-f]{3}$/i.test(c)) {
    const r = parseInt(c[1] + c[1], 16)
    const g = parseInt(c[2] + c[2], 16)
    const b = parseInt(c[3] + c[3], 16)
    return { r, g, b }
  }
  if (/^#[0-9a-f]{6}$/i.test(c)) {
    const r = parseInt(c.slice(1, 3), 16)
    const g = parseInt(c.slice(3, 5), 16)
    const b = parseInt(c.slice(5, 7), 16)
    return { r, g, b }
  }

  return null
}

const revokeVideo = () => {
  if (videoUrl.value) {
    URL.revokeObjectURL(videoUrl.value)
    videoUrl.value = ''
  }
}

const revokeOverlayWebm = () => {
  if (overlayWebmUrl.value) {
    URL.revokeObjectURL(overlayWebmUrl.value)
    overlayWebmUrl.value = ''
  }
}

const onMainVideoPicked = async (ev: Event) => {
  const input = ev.target as HTMLInputElement | null
  const file = input?.files?.[0] || null
  mainVideoFile.value = file
  mainVideoDurationSec.value = null
  if (!file) return
  try {
    const url = URL.createObjectURL(file)
    const v = document.createElement('video')
    v.preload = 'metadata'
    v.src = url
    await new Promise<void>((resolve, reject) => {
      const done = () => {
        v.onloadedmetadata = null
        v.onerror = null
        resolve()
      }
      v.onloadedmetadata = done
      v.onerror = () => reject(new Error('Không đọc được metadata của video'))
    })
    const d = Number(v.duration)
    if (Number.isFinite(d) && d > 0) mainVideoDurationSec.value = d
    URL.revokeObjectURL(url)
  } catch {
    mainVideoDurationSec.value = null
  }
}

const easeOutCubic = (p: number) => 1 - Math.pow(1 - Math.max(0, Math.min(1, p)), 3)

const loadBitmapFromUrl = async (url: string): Promise<ImageBitmap> => {
  const res = await fetch(url)
  if (!res.ok) throw new Error(`Không tải được ảnh overlay: ${url}`)
  const blob = await res.blob()
  return await createImageBitmap(blob)
}

const fitSizeNoUpscale = (srcW: number, srcH: number, maxW: number, maxH: number) => {
  const s = Math.min(1, maxW / Math.max(1, srcW), maxH / Math.max(1, srcH))
  return { w: Math.max(1, Math.round(srcW * s)), h: Math.max(1, Math.round(srcH * s)) }
}

const drawTemplate3OverlayFrame = (
  ctx: CanvasRenderingContext2D,
  bitmaps: ImageBitmap[],
  width: number,
  height: number,
  t: number,
) => {
  const rightPad = Math.max(0, Math.round(Number(template3RightPad.value) || 0))
  const baseY = Math.max(0, Math.round(Number(template3BaseY.value) || 0))
  const offsetY = Math.max(0, Math.round(Number(template3OffsetY.value) || 0))
  const staggerSec = Math.max(0, Number(template3StaggerSec.value) || 0)
  const moveSec = Math.max(0.05, Number(template3MoveSec.value) || 0.55)
  const startXOffset = Math.max(0, Math.round(Number(template3StartXOffset.value) || 0))

  const boxW = Math.max(1, Math.round(Number(template3BoxSize.value) || 1))
  const boxH = boxW
  const dir = template3AnimationDirection.value
  const baseX = dir === 'right-to-left' ? Math.max(0, width - rightPad - boxW) : Math.max(0, rightPad)
  const startX = dir === 'right-to-left' ? width + startXOffset : -boxW - startXOffset

  ctx.clearRect(0, 0, width, height)

  for (let i = 0; i < bitmaps.length; i++) {
    const bm = bitmaps[i]
    const start = i * staggerSec
    const targetY = baseY + i * offsetY
    const y = targetY

    let x = startX
    if (t >= start) {
      const p = easeOutCubic((t - start) / moveSec)
      x = startX + (baseX - startX) * p
    }

    if (x > -boxW && x < width + boxW) {
      // Contain: compute scale to fit image inside box, preserve aspect ratio
      const scale = Math.min(boxW / bm.width, boxH / bm.height)
      const drawW = bm.width * scale
      const drawH = bm.height * scale
      const drawX = x + (boxW - drawW) / 2
      const drawY = y + (boxH - drawH) / 2

      // Draw white rounded rectangle background/border
      ctx.save()
      ctx.strokeStyle = 'white'
      ctx.lineWidth = 2
      const radius = 10
      ctx.beginPath()
      ctx.roundRect(x, y, boxW, boxH, radius)
      ctx.stroke()
      ctx.restore()

      // Clip to rounded rectangle and draw image contained
      ctx.save()
      ctx.beginPath()
      ctx.roundRect(x, y, boxW, boxH, radius)
      ctx.clip()
      ctx.drawImage(bm, drawX, drawY, drawW, drawH)
      ctx.restore()
    }
  }
}

const buildStaticOverlayPng = async (width: number, height: number): Promise<Blob> => {
  const urls = overlayImageUrls.value
  const bitmaps = await Promise.all(urls.map((u) => loadBitmapFromUrl(u)))
  if (!bitmaps.length) throw new Error('Chưa có ảnh sản phẩm để làm overlay')

  const canvas = document.createElement('canvas')
  canvas.width = Math.max(1, width)
  canvas.height = Math.max(1, height)
  const ctx = canvas.getContext('2d', { alpha: true })
  if (!ctx) throw new Error('Canvas context not available')

  const rightPad = Math.max(0, Math.round(Number(template4RightPad.value) || 0))
  const baseY = Math.max(0, Math.round(Number(template4BaseY.value) || 0))
  const offsetY = Math.max(0, Math.round(Number(template4OffsetY.value) || 0))
  const boxW = Math.max(1, Math.round(Number(template4BoxSize.value) || 1))
  const boxH = boxW
  const baseX = Math.max(0, width - rightPad - boxW)

  ctx.clearRect(0, 0, width, height)
  for (let i = 0; i < bitmaps.length; i++) {
    const bm = bitmaps[i]
    const y = baseY + i * offsetY

    // Contain: compute scale to fit image inside box, preserve aspect ratio
    const scale = Math.min(boxW / bm.width, boxH / bm.height)
    const drawW = bm.width * scale
    const drawH = bm.height * scale
    const drawX = baseX + (boxW - drawW) / 2
    const drawY = y + (boxH - drawH) / 2

    // Draw white rounded rectangle background/border
    ctx.save()
    ctx.strokeStyle = 'white'
    ctx.lineWidth = 2
    const radius = 10
    ctx.beginPath()
    ctx.roundRect(baseX, y, boxW, boxH, radius)
    ctx.stroke()
    ctx.restore()

    // Clip to rounded rectangle and draw image contained
    ctx.save()
    ctx.beginPath()
    ctx.roundRect(baseX, y, boxW, boxH, radius)
    ctx.clip()
    ctx.drawImage(bm, drawX, drawY, drawW, drawH)
    ctx.restore()
  }

  return await canvasToPngBlob(canvas)
}

const buildOverlayPngSequence = async (width: number, height: number, safeFps: number, durationSec: number) => {
  const urls = overlayImageUrls.value
  const bitmaps = await Promise.all(urls.map((u) => loadBitmapFromUrl(u)))
  if (!bitmaps.length) throw new Error('Chưa có ảnh sản phẩm để làm overlay')

  const canvas = document.createElement('canvas')
  canvas.width = Math.max(1, width)
  canvas.height = Math.max(1, height)
  const ctx = canvas.getContext('2d', { alpha: true })
  if (!ctx) throw new Error('Canvas context not available')

  const staggerSec = Math.max(0, Number(template3StaggerSec.value) || 0)
  const moveSec = Math.max(0.05, Number(template3MoveSec.value) || 0.55)
  const animatedDurationSec = Math.min(durationSec, Math.max(0.05, (bitmaps.length - 1) * staggerSec + moveSec))
  const frameCount = Math.max(1, Math.ceil(animatedDurationSec * safeFps))
  const frames: Array<{ blob: Blob; fromSec: number; toSec: number; name: string }> = []

  for (let f = 0; f < frameCount; f++) {
    const fromSec = f / safeFps
    const toSec = Math.min(durationSec, (f + 1) / safeFps)
    drawTemplate3OverlayFrame(ctx, bitmaps, width, height, fromSec)
    const blob = await canvasToPngBlob(canvas)
    frames.push({ blob, fromSec, toSec, name: `overlay_${String(f + 1).padStart(4, '0')}.png` })
  }

  if (durationSec > animatedDurationSec) {
    drawTemplate3OverlayFrame(ctx, bitmaps, width, height, animatedDurationSec)
    const blob = await canvasToPngBlob(canvas)
    frames.push({ blob, fromSec: animatedDurationSec, toSec: durationSec, name: `overlay_${String(frameCount + 1).padStart(4, '0')}.png` })
  }

  return frames
}

const buildOverlayWebmBlob = async (width: number, height: number, safeFps: number, durationSec: number): Promise<Blob> => {
  const urls = overlayImageUrls.value
  const bitmaps = await Promise.all(urls.map((u) => loadBitmapFromUrl(u)))
  if (!bitmaps.length) throw new Error('Chưa có ảnh sản phẩm để làm overlay')

  const canvas = document.createElement('canvas')
  canvas.width = Math.max(1, width)
  canvas.height = Math.max(1, height)
  const ctx = canvas.getContext('2d', { alpha: true })
  if (!ctx) throw new Error('Canvas context not available')

  const stream = canvas.captureStream(safeFps)
  const mime = 'video/webm;codecs=vp9'
  if (typeof MediaRecorder === 'undefined') throw new Error('MediaRecorder not supported')
  if (!MediaRecorder.isTypeSupported(mime)) throw new Error(`Trình duyệt không hỗ trợ ${mime} (VP9)`) 

  const rec = new MediaRecorder(stream, { mimeType: mime, videoBitsPerSecond: 6_000_000 })
  const chunks: BlobPart[] = []
  rec.ondataavailable = (e) => {
    if (e.data && e.data.size > 0) chunks.push(e.data)
  }

  const frameCount = Math.ceil(durationSec * safeFps)

  rec.start(250)
  for (let f = 0; f < frameCount; f++) {
    const t = f / safeFps
    drawTemplate3OverlayFrame(ctx, bitmaps, width, height, t)
    await new Promise<void>((r) => setTimeout(r, 1000 / safeFps))
  }
  rec.stop()

  return await new Promise<Blob>((resolve, reject) => {
    rec.onstop = () => resolve(new Blob(chunks, { type: mime }))
    rec.onerror = () => reject(new Error('MediaRecorder error'))
  })
}

const startRenderOverlayWebm = async () => {
  if (isRendering.value || props.loading) return
  if (!canRenderOverlayWebm.value) return

  revokeOverlayWebm()
  renderError.value = ''
  isRendering.value = true

  const { width, height } = renderSize.value
  const safeFps = Math.max(12, Math.min(60, Number(fps.value) || 30))
  const durationSec = Math.max(0.1, Number(overlayDurationSeconds.value) || 0)

  try {
    const hasText = textOverlays.value.some((x) => (x.text ?? '').toString().trim())
    const hasCta = !!ctaEnabled.value

    let blob: Blob
    if (hasCta || hasText) {
      blob = await buildOverlayWebmLoop(width, height)
    } else {
      blob = await buildOverlayWebmBlob(width, height, safeFps, durationSec)
    }
    const url = URL.createObjectURL(blob)
    overlayWebmUrl.value = url
  } catch (e: any) {
    renderError.value = e?.message ?? 'Render overlay WebM thất bại.'
  } finally {
    isRendering.value = false
  }
}

const stopAudio = () => {
  try {
    const el = previewAudioEl.value
    if (el) {
      el.pause()
      el.currentTime = 0
    }
  } catch {
  }

  stopPreview()
}

const formatDuration = (v: number) => {
  const sec = Math.max(0, Math.round(Number(v) || 0))
  const mm = Math.floor(sec / 60)
  const ss = sec % 60
  return `${mm}:${ss.toString().padStart(2, '0')}`
}

const clearAudio = () => {
  stopAudio()
  selectedAudio.value = null
}

const selectAudio = (it: MusicItem) => {
  stopAudio()
  selectedAudio.value = it
}

const playTrack = async (it: MusicItem) => {
  if (isRendering.value) return
  stopAudio()
  selectedAudio.value = it

  try {
    previewAudioEl.value = new Audio(it.audioUrl)
    previewAudioEl.value.preload = 'none'
    previewAudioEl.value.volume = Math.max(0, Math.min(1, Number(audioVolume.value) || 0.6))
    void previewAudioEl.value.play()
  } catch {
  }

  startPreview()
}

watch(
  () => secondsPerSlide.value,
  () => {
    if (previewTimer.value != null) startPreview()
  },
)

watch(
  () => [props.modelValue, ctaEnabled.value] as const,
  () => {
    startCtaPreviewTimer()
  },
  { immediate: true },
)

watch(
  () => audioVolume.value,
  (v) => {
    const vol = Math.max(0, Math.min(1, Number(v) || 0.6))
    try {
      if (previewAudioEl.value) previewAudioEl.value.volume = vol
    } catch {
    }
  },
)

const loadMusic = async () => {
  if (audioLoading.value) return
  if (musicListMode.value === 'favorites') return
  audioError.value = ''
  audioLoading.value = true

  try {
    const data = await api.get<any>('admin/music', {
      params: {
        q: (musicQuery.value ?? '').trim() || undefined,
        topicId: selectedTopicId.value > 0 ? selectedTopicId.value : undefined,
        styleId: selectedStyleId.value > 0 ? selectedStyleId.value : undefined,
        page: musicPage.value,
        pageSize: musicPageSize.value,
      },
    })

    const list = Array.isArray(data?.items) ? data.items : []
    musicTotal.value = Number(data?.total) || 0
    audioResults.value = list
      .map((x: any) => ({
        id: (x?.id ?? '').toString(),
        name: (x?.name ?? '').toString(),
        author: (x?.author ?? '').toString(),
        duration: Number(x?.durationSeconds) || 0,
        audioUrl: (x?.audioUrl ?? '').toString(),
      }))
      .filter((x: MusicItem) => !!x.id && !!x.audioUrl)
  } catch (e: any) {
    audioError.value = e?.message ?? 'Không tìm được audio.'
  } finally {
    audioLoading.value = false
  }
}

const resetMusicPageAndLoad = () => {
  musicPage.value = 1
  void loadMusic()
}

const musicTotalPages = computed(() => {
  const total = Math.max(0, Number(musicTotal.value) || 0)
  const size = Math.max(1, Number(musicPageSize.value) || 10)
  return Math.max(1, Math.ceil(total / size))
})

const canPrevMusic = computed(() => musicPage.value > 1)
const canNextMusic = computed(() => musicPage.value < musicTotalPages.value)

const prevMusic = () => {
  if (!canPrevMusic.value) return
  musicPage.value = Math.max(1, musicPage.value - 1)
  void loadMusic()
}

const nextMusic = () => {
  if (!canNextMusic.value) return
  musicPage.value = Math.max(1, musicPage.value + 1)
  void loadMusic()
}

const loadMusicMeta = async () => {
  try {
    const [topics, styles] = await Promise.all([
      api.get<any>('admin/music/topics'),
      api.get<any>('admin/music/styles'),
    ])

    musicTopics.value = Array.isArray(topics)
      ? topics
          .map((x: any) => ({ id: Number(x?.id) || 0, name: (x?.name ?? '').toString() }))
          .filter((x: MusicTopicItem) => x.id > 0 && x.name)
      : []

    musicStyles.value = Array.isArray(styles)
      ? styles
          .map((x: any) => ({ id: Number(x?.id) || 0, name: (x?.name ?? '').toString() }))
          .filter((x: MusicStyleItem) => x.id > 0 && x.name)
      : []

    if (!selectedStyleId.value || selectedStyleId.value <= 0) selectedStyleId.value = 162
    if (selectedStyleId.value === 162 && !musicStyles.value.some((x) => x.id === 162)) selectedStyleId.value = 0
  } catch {
    musicTopics.value = []
    musicStyles.value = []
  }
}

const thumbStripEl = ref<HTMLElement | null>(null)

const scrollActiveThumbIntoView = async () => {
  if (!props.modelValue) return
  await nextTick()
  const el = thumbStripEl.value
  if (!el) return
  const btn = el.querySelectorAll<HTMLButtonElement>('.thumb-item')?.[activeIndex.value]
  if (!btn) return
  window.requestAnimationFrame(() => {
    try {
      btn.scrollIntoView({ behavior: 'smooth', block: 'nearest', inline: 'center' })
    } catch {
      // ignore
    }
  })
}

watch(
  () => activeIndex.value,
  () => {
    void scrollActiveThumbIntoView()
  },
  { flush: 'post' },
)

watch(
  () => props.modelValue,
  (open) => {
    if (open) void scrollActiveThumbIntoView()
  },
  { flush: 'post' },
)

watch(
  () => [props.imageUrls, props.modelValue] as const,
  () => {
    const baseImages = [...(props.imageUrls ?? [])]
    // Add default GIF at the end
    baseImages.push('https://media.giphy.com/media/v1.Y2lkPTc5MGI3NjExM3R0d2FqeGh3bXl3d2R6Z2h4d2Z3dXlqdjQ4a2Vnb2VvZnlmbyZlcD12MV9pbnRlcm5hbF9naWZfYnlfaWQmY3Q9Zw/LmVwrqZ4NmfT2/giphy.gif')
    orderedImages.value = baseImages
    activeIndex.value = 0
    draggingImageUrl.value = ''
    renderError.value = ''
    if (!props.modelValue || props.imageUrls.length === 0) revokeVideo()
  },
  { immediate: true },
)

watch(
  () => props.modelValue,
  (open) => {
    if (!open) stopAudio()
    if (open) {
      isImagesOpen.value = true
      isAudioOpen.value = true
      isTextOpen.value = true
      musicPage.value = 1
      void loadMusicMeta()
      void loadMusic()
    }
  },
  { immediate: true }
)

watch(
  () => [props.modelValue, dialogEl.value] as const,
  async ([open, dlg]) => {
    if (!dlg) return

    if (open) {
      if (!dlg.open) {
        dlg.showModal()
      }
      await nextTick()
      try {
        dlg.querySelector<HTMLElement>('button, [href], input, select, textarea, [tabindex]:not([tabindex="-1"])')?.focus()
      } catch {
      }
    } else {
      if (dlg.open) dlg.close()
    }
  },
  { flush: 'post', immediate: true },
)

watch(
  () => orderedImages.value.length,
  (len) => {
    if (len <= 0) {
      activeIndex.value = 0
      return
    }
    if (activeIndex.value >= len) activeIndex.value = len - 1
  },
)

onMounted(() => {
})

const close = () => {
  if (isRendering.value) return
  emit('update:modelValue', false)
}

const selectPrev = () => {
  if (orderedImages.value.length === 0) return
  activeIndex.value = activeIndex.value <= 0 ? orderedImages.value.length - 1 : activeIndex.value - 1
}

const selectNext = () => {
  if (orderedImages.value.length === 0) return
  activeIndex.value = activeIndex.value >= orderedImages.value.length - 1 ? 0 : activeIndex.value + 1
}

const removeImageAt = (idx: number) => {
  if (isRendering.value) return
  const list = [...orderedImages.value]
  if (idx < 0 || idx >= list.length) return
  list.splice(idx, 1)
  orderedImages.value = list
  if (list.length === 0) {
    activeIndex.value = 0
    return
  }
  if (activeIndex.value > idx) {
    activeIndex.value = activeIndex.value - 1
  } else if (activeIndex.value === idx) {
    activeIndex.value = Math.min(activeIndex.value, list.length - 1)
  }
}

const moveImageBefore = (sourceUrl: string, targetUrl: string) => {
  if (!sourceUrl || !targetUrl || sourceUrl === targetUrl) return
  const list = [...orderedImages.value]
  const fromIndex = list.indexOf(sourceUrl)
  const toIndex = list.indexOf(targetUrl)
  if (fromIndex < 0 || toIndex < 0 || fromIndex === toIndex) return
  const [moved] = list.splice(fromIndex, 1)
  const insertIndex = list.indexOf(targetUrl)
  if (!moved || insertIndex < 0) return
  list.splice(insertIndex, 0, moved)
  orderedImages.value = list
  activeIndex.value = list.indexOf(sourceUrl)
}

const onImageDragStart = (url: string) => {
  if (isRendering.value) return
  draggingImageUrl.value = url
}

const onImageDragOver = (_url: string) => {
}

const onImageDrop = (url: string) => {
  if (!draggingImageUrl.value) return
  moveImageBefore(draggingImageUrl.value, url)
  draggingImageUrl.value = ''
}

const onImageDragEnd = () => {
  draggingImageUrl.value = ''
}

const parseSize = () => {
  const [widthText, heightText] = selectedSize.value.split('x')
  const width = Number(widthText) || 720
  const height = Number(heightText) || 1280
  return { width, height }
}

const startRender = async () => {
  if (isRendering.value || props.loading) return
  if (orderedImages.value.length === 0) return

  revokeVideo()
  renderError.value = ''
  isRendering.value = true

  try {
    if (Number(selectedTemplate.value) === 2) {
      await computeTemplate2HeightFromImages()
    }

    if (Number(selectedTemplate.value) === 3) {
      if (!mainVideoFile.value) {
        renderError.value = 'Vui lòng chọn Main video.'
        return
      }

      const { width, height } = renderSize.value
      const safeFps = Math.max(12, Math.min(60, Number(fps.value) || 30))
      const durationSec = Math.max(0.1, Number(overlayDurationSeconds.value) || 0)

      const overlayFrames = await buildOverlayPngSequence(width, height, safeFps, durationSec)
      const form = new FormData()
      form.append(
        'req',
        JSON.stringify({
          productName: props.productName ?? '',
          width,
          height,
          fps: safeFps,
        }),
      )
      form.append('mainVideo', mainVideoFile.value, mainVideoFile.value.name)
      form.append(
        'overlaySegments',
        JSON.stringify(
          overlayFrames.map((x) => ({
            fromSec: x.fromSec,
            toSec: x.toSec,
          })),
        ),
      )
      for (const frame of overlayFrames) {
        form.append('overlayPngs', frame.blob, frame.name)
      }

      const blob = (await api.request<any>({
        method: 'POST',
        url: 'admin/products/render-reels-reupvideo',
        data: form,
        headers: { 'Content-Type': 'multipart/form-data' },
        responseType: 'blob',
        timeout: 900000,
      })) as Blob

      const url = URL.createObjectURL(blob)
      videoUrl.value = url
      emit('rendered', { blob, url, images: [...orderedImages.value] })
      return
    }

    if (Number(selectedTemplate.value) === 4) {
      if (!mainVideoFile.value) {
        renderError.value = 'Vui lòng chọn Main video.'
        return
      }

      const { width, height } = renderSize.value
      const safeFps = Math.max(12, Math.min(60, Number(fps.value) || 30))
      const durationSec = Math.max(0.1, Number(overlayDurationSeconds.value) || 0)

      const overlayPng = await buildStaticOverlayPng(width, height)
      const form = new FormData()
      form.append(
        'req',
        JSON.stringify({
          productName: props.productName ?? '',
          width,
          height,
          fps: safeFps,
        }),
      )
      form.append('mainVideo', mainVideoFile.value, mainVideoFile.value.name)
      form.append('overlaySegments', JSON.stringify([{ fromSec: 0, toSec: durationSec }]))
      form.append('overlayPngs', overlayPng, 'overlay_static.png')

      const blob = (await api.request<any>({
        method: 'POST',
        url: 'admin/products/render-reels-reupvideo',
        data: form,
        headers: { 'Content-Type': 'multipart/form-data' },
        responseType: 'blob',
        timeout: 900000,
      })) as Blob

      const url = URL.createObjectURL(blob)
      videoUrl.value = url
      emit('rendered', { blob, url, images: [...orderedImages.value] })
      return
    }

    const { width, height } = renderSize.value

    const safeSeconds = Math.max(1, Math.min(10, Number(secondsPerSlide.value) || 3))
    const safeFps = Math.max(12, Math.min(60, Number(fps.value) || 30))

    const hasText = textOverlays.value.some((x) => (x.text ?? '').toString().trim())
    const reqPayload = {
      productName: props.productName ?? '',
      imageUrls: [...orderedImages.value],
      template: Number(selectedTemplate.value) || 1,
      width,
      height,
      fps: safeFps,
      secondsPerSlide: safeSeconds,
      transitionSeconds: 0.35,
      audioPreviewUrl: selectedAudio.value?.audioUrl || null,
      audioVolume: Math.max(0, Math.min(2, Number(audioVolume.value) || 0.6)),
    }

    let blob: Blob
    if (!hasText) {
      blob = (await api.request<any>({
        method: 'POST',
        url: 'admin/products/render-reels',
        data: reqPayload,
        responseType: 'blob',
        timeout: 300000,
      })) as Blob
    } else {
      isOverlayRendering.value = true
      const form = new FormData()
      form.append('req', JSON.stringify({ ...reqPayload }))

      const hasOverlayAnim = textOverlays.value.some((x: any) => ((x.animation ?? 'none').toString().toLowerCase() !== 'none'))
      if (useAnimatedOverlay.value && (ctaEnabled.value || hasOverlayAnim)) {
        const overlayWebm = await buildOverlayWebmLoop(width, height)
        form.append('overlayWebm', overlayWebm, 'overlay_loop.webm')
      } else {
        const overlays = textOverlays.value
          .map((x) => ({
            id: x.id,
            text: (x.text ?? '').toString().trim(),
            fromSec: Math.max(0, Number(x.fromSec) || 0),
            toSec: Math.max(0, Number(x.toSec) || 0),
          }))
          .filter((x) => x.text)

        const segments = overlays.map((x) => ({
          fromSec: x.fromSec,
          toSec: Math.max(x.fromSec, x.toSec),
        }))

        form.append('overlaySegments', JSON.stringify(segments))

        for (const ov of textOverlays.value) {
          const t = (ov.text ?? '').toString().trim()
          if (!t) continue
          const blob = await buildOverlayPngForOverlay(ov, width, height)
          form.append('overlayPngs', blob, `${ov.id}.png`)
        }
      }

      blob = (await api.request<any>({
        method: 'POST',
        url: 'admin/products/render-reels-overlay',
        data: form,
        headers: { 'Content-Type': 'multipart/form-data' },
        responseType: 'blob',
        timeout: 600000,
      })) as Blob
    }

    const url = URL.createObjectURL(blob)
    videoUrl.value = url
    emit('rendered', { blob, url, images: [...orderedImages.value] })
  } catch (e: any) {
    renderError.value = e?.message ?? 'Render video thất bại.'
  } finally {
    isRendering.value = false
    isOverlayRendering.value = false
  }
}

onBeforeUnmount(() => {
  document.removeEventListener('click', handleClickOutside)
  stopTemplate3PreviewTimer()
  stopCtaPreviewTimer()
  stopAudio()
  revokeVideo()
  revokeOverlayWebm()
  try {
    if (dialogEl.value?.open) dialogEl.value.close()
  } catch {
  }
})
</script>

<style scoped>

.dialog {
  border: 0;
  padding: 0;
  background: transparent;
  max-width: calc(100vw - 24px);
  max-height: calc(100vh - 24px);
}

.dialog::backdrop {
  background: rgba(0, 0, 0, 0.45);
}

.modal {
  width: 100%;
  max-width: 1280px;
  background: #fff;
  border-radius: 16px;
  overflow: hidden;
  border: 1px solid #eee;
  display: flex;
  flex-direction: column;
  max-height: 92vh;
}

.modal.modal-wide {
  min-width: 1040px;
}

.modal-header {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  padding: 14px 16px;
  border-bottom: 1px solid #eee;
}

.modal-title {
  font-weight: 800;
  font-size: 18px;
}

.icon {
  width: 34px;
  height: 34px;
  border-radius: 10px;
  border: 1px solid #eee;
  background: #fff;
  font-size: 18px;
  cursor: pointer;
}

.modal-body {
  padding: 16px;
  overflow: hidden;
  flex: 1;
  min-height: 0;
}

.layout {
  display: grid;
  grid-template-columns: minmax(420px, 1.15fr) minmax(420px, 1fr);
  gap: 16px;
  height: 100%;
  min-height: 0;
}

.panel {
  border: 1px solid #e5e7eb;
  border-radius: 14px;
  background: #fff;
  padding: 14px;
  display: grid;
  gap: 12px;
  align-content: start;
  overflow: auto;
  min-height: 0;
  max-height: 800px;
}
.panel-content{
  min-width: 660px;
}
.preview-panel {
  overflow: auto;
  min-height: 0;
}

.panel-head {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 12px;
}

.section-head {
  border: 0;
  background: transparent;
  padding: 0;
  cursor: pointer;
  width: 100%;
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 12px;
  text-align: left;
}

.section-head-right {
  display: inline-flex;
  align-items: center;
  justify-content: flex-end;
  gap: 10px;
  flex: 0 0 auto;
}

.chev {
  width: 20px;
  height: 20px;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  color: #667085;
  transform: rotate(-90deg);
  transition: transform 120ms ease;
}

.chev.open {
  transform: rotate(0deg);
}

.panel-actions {
  display: flex;
  gap: 8px;
}

.section-title {
  font-size: 14px;
  font-weight: 700;
  color: #111827;
}

.muted {
  color: #667085;
  font-size: 13px;
  margin-top: 4px;
}

.image-grid {
  display: grid;
  grid-template-columns: repeat(4, minmax(0, 1fr));
  gap: 10px;
}

.image-card {
  border: 1px solid #e5e7eb;
  border-radius: 14px;
  background: #fff;
  padding: 8px;
  display: grid;
  gap: 8px;
  cursor: pointer;
  position: relative;
}

.remove-image {
  width: 26px;
  height: 26px;
  border-radius: 10px;
  border: 1px solid #e5e7eb;
  background: rgba(255, 255, 255, 0.95);
  color: #111827;
  font-size: 18px;
  line-height: 1;
  cursor: pointer;
}

.remove-image:disabled {
  opacity: 0.55;
  cursor: not-allowed;
}

.image-card.active {
  border-color: #111827;
  box-shadow: 0 0 0 1px rgba(17, 24, 39, 0.08);
}

.image-card.dragging {
  opacity: 0.55;
}

.image-card-top {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 8px;
}

.order-badge,
.active-badge {
  font-size: 11px;
  font-weight: 700;
  border-radius: 999px;
  padding: 4px 8px;
}

.order-badge {
  background: #f2f4f7;
  color: #344054;
}

.active-badge {
  background: #111827;
  color: #fff;
}

.image-thumb {
  width: 100%;
  aspect-ratio: 1 / 1;
  object-fit: cover;
  border-radius: 10px;
  border: 1px solid #eee;
  background: #f5f5f5;
}

.preview-stage {
  display: flex;
  justify-content: center;
}

.phone-frame {
  width: min(100%, 420px);
  border-radius: 28px;
  padding: 14px;
  background: linear-gradient(180deg, #0f172a 0%, #111827 100%);
  display: grid;
  grid-template-rows: auto auto;
  gap: 6px;
  box-shadow: inset 0 0 0 1px rgba(255, 255, 255, 0.05);
  position: relative;
}

.preview-viewport {
  border-radius: 20px;
  overflow: hidden;
  background: rgba(255, 255, 255, 0.06);
  display: flex;
  flex-direction: column;
  justify-content: center;
  align-items: stretch;
  width: 100%;
  position: relative;
}

.preview-viewport img {
  width: 100%;
  height: auto;
  display: block;
}

.preview-viewport .thumb-strip {
  justify-content: center;
  margin-top: 8px;
}

.preview-viewport .empty-preview {
  position: absolute;
  inset: 0;
  min-height: unset;
}

.empty-preview {
  display: flex;
  align-items: center;
  justify-content: center;
  color: rgba(255, 255, 255, 0.75);
  min-height: 220px;
}

.thumb-strip {
  display: flex;
  gap: 8px;
  overflow-x: auto;
  padding-bottom: 0;
  scroll-snap-type: x mandatory;
  -webkit-overflow-scrolling: touch;
  overscroll-behavior-x: contain;
  position: relative;
  z-index: 2;
}

.thumb-item {
  scroll-snap-align: center;
  padding: 0;
  border: 2px solid rgba(255, 255, 255, 0.18);
  border-radius: 14px;
  overflow: hidden;
  background: transparent;
  cursor: pointer;
  flex: 0 0 74px;
}

.thumb-item.active {
  border-color: #ef4444;
  box-shadow: 0 0 0 2px rgba(239, 68, 68, 0.18);
}

.thumb-strip::-webkit-scrollbar {
  height: 0;
}

.fade-enter-active,
.fade-leave-active {
  transition: opacity 160ms ease;
}

.fade-enter-from,
.fade-leave-to {
  opacity: 0;
}

.thumb-item img {
  width: 100%;
  aspect-ratio: 1 / 1;
  object-fit: cover;
  display: block;
}

.overlay-layer {
  position: absolute;
  inset: 0;
  pointer-events: none;
  z-index: 5;
}

.overlay-img {
  position: absolute;
  pointer-events: none;
}

.overlay-item {
  position: absolute;
  font-weight: 400;
  font-family: sans-serif;
  line-height: 1.15;
  white-space: pre-line;
  text-shadow: 0 2px 14px rgba(0, 0, 0, 0.55);
}

.overlay-render-root {
  position: fixed;
  left: -10000px;
  top: -10000px;
  width: 1px;
  height: 1px;
  overflow: hidden;
}

.overlay-render-iframe {
  width: 1px;
  height: 1px;
  border: 0;
  background: transparent;
}

.overlay-render-stage {
  position: relative;
  background: transparent;
}

.overlay-render-item {
  position: absolute;
  font-weight: 400;
  line-height: 1.15;
  white-space: pre-line;
  text-shadow: 0 2px 14px rgba(0, 0, 0, 0.55);
  max-width: 92%;
}

.config-grid {
  display: grid;
  grid-template-columns: repeat(3, minmax(0, 1fr));
  gap: 10px;
}

.lbl {
  font-size: 12px;
  color: #667085;
  margin-bottom: 6px;
}

.input {
  height: 36px;
  border: 1px solid #e5e7eb;
  border-radius: 10px;
  padding: 0 12px;
  outline: none;
  width: 100%;
}

.summary-row {
  display: flex;
  flex-wrap: wrap;
  gap: 12px;
}

.summary-item {
  font-size: 13px;
  color: #667085;
}

.summary-item .k {
  color: #98a2b3;
}

.summary-item .v {
  color: #111827;
  font-weight: 700;
}

.result-box {
  display: grid;
  gap: 10px;
}

.video-player {
  width: 100%;
  max-height: 360px;
  border-radius: 14px;
  background: #000;
}

.result-actions {
  display: flex;
  justify-content: flex-end;
  gap: 10px;
}

.btn {
  height: 36px;
  border-radius: 10px;
  padding: 0 12px;
  border: 1px solid #111827;
  background: #111827;
  color: #fff;
  cursor: pointer;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  text-decoration: none;
}

.btn.secondary {
  background: #fff;
  color: #111827;
}

.btn.tiny {
  height: 28px;
  padding: 0 10px;
  border-radius: 10px;
  font-size: 12px;
}

.btn:disabled,
.icon:disabled {
  opacity: 0.65;
  cursor: not-allowed;
}

.empty-state {
  border: 1px dashed #d0d5dd;
  border-radius: 14px;
  padding: 24px;
  text-align: center;
  color: #667085;
}

.audio-box {
  border-top: 1px solid #eef2f7;
  padding-top: 12px;
  display: grid;
  gap: 10px;
}

.text-box {
  border-top: 1px solid #eef2f7;
  padding-top: 12px;
  display: grid;
  gap: 10px;
}

.text-head {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 12px;
}

.text-list {
  display: grid;
  gap: 10px;
}

.text-item {
  border: 1px solid #e5e7eb;
  border-radius: 12px;
  padding: 10px;
  display: grid;
  gap: 10px;
}

.text-row {
  display: grid;
  grid-template-columns: 1fr auto;
  gap: 8px;
  align-items: center;
}

.text-grid {
  
    display: flex;
    flex-wrap: wrap;
    justify-content: space-between;

}

.anchor-controls {
  grid-column: 1 / -1;
  margin-bottom: 8px;
}

.align-controls {
  display: grid;
  gap: 4px;
}

.align-buttons {
  display: inline-flex;
  gap: 4px;
}

.align-btn {
  width: 36px;
  height: 36px;
  border: 1px solid #d1d5db;
  background: #fff;
  border-radius: 6px;
  cursor: pointer;
  display: flex;
  align-items: center;
  justify-content: center;
  transition: all 0.15s;
  color: #111827;
}

.align-btn svg {
  width: 18px;
  height: 18px;
  display: block;
}

.align-btn:hover:not(:disabled) {
  background: #f3f4f6;
  border-color: #9ca3af;
}

.align-btn.active {
  background: #3b82f6;
  color: #fff;
  border-color: #3b82f6;
}

.align-btn:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}

.anchor-group .lbl {
  font-size: 12px;
  margin-bottom: 4px;
  color: #6b7280;
}

.anchor-buttons {
  display: grid;
  grid-template-columns: repeat(3, 1fr);
  gap: 4px;
  width: 120px;
}

.anchor-btn {
  width: 36px;
  height: 36px;
  border: 1px solid #d1d5db;
  background: white;
  border-radius: 6px;
  cursor: pointer;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 16px;
  transition: all 0.15s;
}

.anchor-btn:hover:not(:disabled) {
  background: #f3f4f6;
  border-color: #9ca3af;
}

.anchor-btn.active {
  background: #3b82f6;
  color: white;
  border-color: #3b82f6;
}

.anchor-btn:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}

.small .input {
  width: 60px;
  padding: 4px 6px;
  font-size: 12px;
}

.color-input {
  width: 60px;
  height: 28px;
  padding: 0;
  border: 1px solid #d1d5db;
  border-radius: 4px;
  cursor: pointer;
}

.style-controls {
  display: flex;
  gap: 4px;
}

.style-btn {
  width: 28px;
  height: 28px;
  border: 1px solid #d1d5db;
  background: white;
  border-radius: 4px;
  cursor: pointer;
  font-weight: bold;
  font-size: 12px;
  transition: all 0.15s;
}

.style-btn:hover:not(:disabled) {
  background: #f3f4f6;
  border-color: #9ca3af;
}

.style-btn.active {
  background: #3b82f6;
  color: white;
  border-color: #3b82f6;
}

.style-btn:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}

.text-box-inline {
  display: inline-flex;
  align-items: center;
  gap: 8px;
  height: 36px;
}

.overlay-textarea {
  min-height: 60px;
  resize: vertical;
}

.time-row {
  margin-top: 8px;
  display: flex;
  gap: 10px;
  align-items: end;
}

.textarea-wrapper {
  position: relative;
  width: 100%;
}

.icon-picker-dropdown {
  position: relative;
  display: inline-block;
}

.icon-dropdown-panel {
  position: absolute;
  margin-bottom: 4px;
  background: white;
  border: 1px solid #e5e7eb;
  border-radius: 8px;
  box-shadow: 0 10px 25px -5px rgba(0, 0, 0, 0.1);
  z-index: 100;
  max-height: 300px;
  overflow-y: auto;
  min-width: 320px;
  padding: 8px;
}

.icon-dropdown-panel .icon-grid {
  display: grid;
  grid-template-columns: repeat(6, 1fr);
  gap: 4px;
}

.icon-picker-btn {
  width: 32px;
  height: 32px;
  border: 1px solid #d1d5db;
  background: white;
  border-radius: 6px;
  cursor: pointer;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 16px;
  transition: all 0.15s;
  z-index: 101;
}

.icon-picker-btn:hover:not(:disabled) {
  background: #f3f4f6;
  border-color: #9ca3af;
}

.icon-picker-btn:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}

.icon-preview {
  font-size: 18px;
}

.icon-placeholder {
  font-size: 14px;
  opacity: 0.6;
}

.icon-picker-panel {
  margin-top: 8px;
  padding: 12px;
  border: 1px solid #e5e7eb;
  border-radius: 8px;
  background: #f9fafb;
}

.icon-grid {
  display: grid;
  grid-template-columns: repeat(6, 1fr);
  gap: 8px;
  margin-bottom: 12px;
}

.icon-option {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 4px;
  padding: 8px;
  border: 1px solid #e5e7eb;
  background: white;
  border-radius: 6px;
  cursor: pointer;
  transition: all 0.15s;
}

.icon-option:hover:not(:disabled) {
  background: #f3f4f6;
  border-color: #9ca3af;
}

.icon-option.active {
  background: #3b82f6;
  border-color: #3b82f6;
}

.icon-char {
  font-size: 20px;
}

.icon-name {
  font-size: 10px;
  color: #6b7280;
}

.icon-option.active .icon-name {
  color: white;
}

.icon-color-section {
  display: flex;
  justify-content: flex-end;
}

.overlay-icon {
  margin-right: 4px;
  font-size: 1.1em;
}

.audio-head {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
}

.audio-search {
  display: grid;
  grid-template-columns: 1fr auto auto;
  gap: 10px;
  align-items: center;
}

.audio-results {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 8px;
}

.audio-item {
  border: 1px solid #e5e7eb;
  border-radius: 12px;
  background: #fff;
  padding: 10px;
  text-align: left;
  cursor: pointer;
  display: grid;
  gap: 4px;
  overflow: hidden;
}

.audio-item.active {
  border-color: #111827;
  box-shadow: 0 0 0 1px rgba(17, 24, 39, 0.08);
}

.audio-item-title {
  font-size: 13px;
  font-weight: 700;
  color: #111827;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.audio-item-row {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 10px;
  min-width: 0;
  width: 100%;
}

.audio-item-actions {
  display: inline-flex;
  align-items: center;
  justify-content: flex-end;
  gap: 6px;
  flex: 0 0 auto;
}

.audio-item-title {
  min-width: 0;
  flex: 1 1 auto;
  max-width: 100%;
}

.audio-item-meta {
  font-size: 12px;
  color: #667085;
}

.audio-volume {
  display: grid;
  grid-template-columns: 120px 1fr;
  gap: 10px;
  align-items: center;
}

.error {
  color: #b42318;
  background: #fef3f2;
  border: 1px solid #fecdca;
  padding: 10px 12px;
  border-radius: 12px;
}

.modal-footer {
  padding: 12px 16px;
  border-top: 1px solid #eee;
  display: flex;
  gap: 10px;
  justify-content: flex-end;
}

@media (max-width: 1180px) {
  .modal.modal-wide {
    min-width: 0;
  }

  .layout {
    grid-template-columns: 1fr;
  }

  .preview-panel {
    position: static;
  }
}

@media (max-width: 680px) {
  .image-grid {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }

  .config-grid {
    grid-template-columns: 1fr;
  }
}
</style>
