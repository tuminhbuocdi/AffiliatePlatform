<template>
  <div class="yt-admin-page">
    <div class="page-header">
      <div class="breadcrumb">
        <span>Trang chủ</span>
        <span class="sep">/</span>
        <span>Admin</span>
        <span class="sep">/</span>
        <span class="current">YouTube</span>
      </div>

      <div class="header-row">
        <h1 class="page-title">YouTube Channels</h1>

        <div class="header-actions">
          <button class="btn" type="button" :disabled="connecting" @click="connectChannel">
            {{ connecting ? 'Đang kết nối...' : 'Kết nối kênh mới' }}
          </button>
          <button class="btn secondary" type="button" :disabled="loading" @click="reload">
            Tải lại
          </button>
        </div>
      </div>

      <button class="upload-fab" type="button" :disabled="channels.length === 0" @click="handleUploadClick">
        <span class="fab-icon">+</span>
        <span class="fab-label">Upload</span>
      </button>
      <button class="upload-fab bulk" type="button" :disabled="channels.length === 0" @click="handleBulkUploadClick">
        <span class="fab-icon">+</span>
        <span class="fab-label">Upload hàng loạt</span>
      </button>

      <div class="tabs">
        <button type="button" class="tab" :class="{ active: activeTab === 'channels' }" @click="activeTab = 'channels'">
          Kênh
        </button>
        <button
          type="button"
          class="tab"
          :class="{ active: activeTab === 'videos' }"
          @click="activeTab = 'videos'"
        >
          Video
        </button>
        <button
          type="button"
          class="tab"
          :class="{ active: activeTab === 'analytics' }"
          @click="activeTab = 'analytics'"
        >
          Analytics
        </button>
      </div>
    </div>

    <div v-if="message" :class="['msg', messageType]">{{ message }}</div>

    <div class="tab-layout">
      <div v-if="activeTab === 'channels'" class="card tab-main">
        <div class="card-title">Danh sách kênh</div>

        <div v-if="loading" class="muted">Đang tải...</div>

        <div v-else-if="channels.length === 0" class="muted">Chưa có kênh nào.</div>

        <div v-else class="table-wrap">
          <table class="table">
            <thead>
              <tr>
                <th>Kênh</th>
                <th style="width: 160px">Trạng thái</th>
                <th style="width: 240px">Cập nhật</th>
                <th style="width: 170px"></th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="c in channels" :key="c.id">
                <td>
                  <div class="channel-cell">
                    <img v-if="c.thumbnailUrl" class="thumb" :src="c.thumbnailUrl" alt="thumbnail" />
                    <div>
                      <div class="channel-title">{{ c.title }}</div>
                      <div class="channel-sub">{{ c.id }}</div>
                    </div>
                  </div>
                </td>
                <td>
                  <span :class="['badge', c.isAuthorized ? 'ok' : 'warn']">
                    {{ c.isAuthorized ? 'Đã auth' : 'Chưa auth' }}
                  </span>
                </td>
                <td>{{ formatDateTime(c.updatedAt) }}</td>
                <td class="row-actions">
                  <button class="btn tiny" type="button" :disabled="workingId === c.id" @click="reauthorize(c.id)">
                    {{ workingId === c.id ? '...' : 'Auth lại' }}
                  </button>
                  <button class="btn tiny danger" type="button" :disabled="workingId === c.id" @click="disconnect(c.id)">
                    Xoá
                  </button>
                </td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>

      <div v-else-if="activeTab === 'videos'" class="card video-main-card tab-main">
        <div class="video-toolbar">
          <div class="toolbar-left">
            <div class="field" style="min-width: 260px; max-width: 520px">
              <label>Chọn kênh</label>
              <SelectMultiTags v-model="videoSelectedChannelIds" :options="channelOptions" placeholder="Chọn kênh..." :disabled="loading" />
            </div>
            <div class="field" style="max-width: 420px">
              <input v-model="videoQuery" type="text" class="input" placeholder="Tìm theo title..." @keyup.enter="loadVideos" />
            </div>

            <div class="bulk-info">
              <span class="bulk-title">Đã chọn: {{ selectedVideoIds.size }}</span>
              <button class="btn tiny secondary" type="button" :disabled="videoLoading || videos.length === 0" @click="toggleVideoSelectAll(!videoAllSelected)">
                {{ videoAllSelected ? 'Bỏ chọn tất cả' : 'Chọn tất cả' }}
              </button>

              <button v-if="selectedVideoIds.size > 0" class="btn tiny secondary" type="button" @click="clearSelection">Bỏ chọn</button>

              <select v-model="bulkPlaylistId" class="input" style="max-width: 200px" :disabled="playlistsLoading">
                <option value="">Chọn playlist...</option>
                <option v-for="p in playlists" :key="p.id" :value="p.id">{{ p.title }}</option>
              </select>

              <button v-if="selectedVideoIds.size > 0" class="btn tiny" type="button" :disabled="bulkWorking || !bulkPlaylistId || videoSelectedChannelIds.length !== 1" @click="bulkAddToPlaylist">
                {{ bulkWorking ? '...' : 'Thêm vào playlist' }}
              </button>

              <button v-if="selectedVideoIds.size > 0" class="btn tiny" type="button" :disabled="bulkWorking" @click="openBulkEdit">Sửa hàng loạt</button>
            </div>
          </div>
          <div class="toolbar-actions">
            <div class="view-toggle">
              <button type="button" class="toggle" :class="{ active: videoViewMode === 'cards' }" @click="videoViewMode = 'cards'">
                Cards
              </button>
              <button type="button" class="toggle" :class="{ active: videoViewMode === 'list' }" @click="videoViewMode = 'list'">
                List
              </button>
            </div>
            <button class="btn" type="button" :disabled="videoLoading || videoSelectedChannelIds.length === 0" @click="loadVideos">
              {{ videoLoading ? 'Đang tải...' : 'Tải danh sách' }}
            </button>
            <button class="btn secondary" type="button" @click="openScanLinks">
              Rà soát link
            </button>
            <button class="btn secondary" type="button" :disabled="videoSelectedChannelIds.length !== 1" @click="openPlaylistManager">
              Playlist
            </button>
          </div>
        </div>

        <div v-if="videoLoading" class="muted">Đang tải...</div>
        <div v-else-if="videos.length === 0" class="muted">Chưa có video nào (hoặc không tải được).</div>

        <div v-else>
          <div v-if="videoViewMode === 'list'" class="table-wrap">
            <table class="table">
              <thead>
                <tr>
                  <th style="width: 42px">
                    <input type="checkbox" :checked="videoAllSelected" :disabled="videos.length === 0" @change="toggleVideoSelectAll(($event.target as HTMLInputElement).checked)" />
                  </th>
                  <th>Video</th>
                  <th style="width: 110px">View</th>
                  <th style="width: 110px">Comment</th>
                  <th style="width: 110px">Like</th>
                  <th style="width: 110px">Unlike</th>
                  <th style="width: 140px">Trạng thái</th>
                  <th style="width: 180px">Xuất bản</th>
                  <th style="width: 220px"></th>
                </tr>
              </thead>
              <tbody>
                <tr v-for="v in videos" :key="v.key" :class="{ selected: selectedVideoIds.has(v.key) }">
                  <td>
                    <input type="checkbox" :checked="selectedVideoIds.has(v.key)" @change="toggleVideoSelection(v.key, ($event.target as HTMLInputElement).checked)" />
                  </td>
                  <td>
                    <div class="video-cell">
                      <img v-if="v.thumbnailUrl" class="thumb" :src="v.thumbnailUrl" alt="thumb" />
                      <div>
                        <div class="channel-title">{{ v.title }}</div>
                        <div class="channel-sub">{{ v.id }} • {{ v.channelId }}</div>
                      </div>
                    </div>
                  </td>
                  <td>{{ formatCount(v.viewCount) }}</td>
                  <td>{{ formatCount(v.commentCount) }}</td>
                  <td>{{ formatCount(v.likeCount) }}</td>
                  <td>{{ formatCount(v.dislikeCount) }}</td>
                  <td>
                    <span :class="['badge', v.privacyStatus === 'public' ? 'ok' : 'warn']">
                      {{ v.privacyStatus }}
                    </span>
                  </td>
                  <td>{{ formatDateTime(v.publishedAt) }}</td>
                  <td class="row-actions">
                    <button class="btn tiny" type="button" :disabled="workingId === v.key || videoSelectedChannelIds.length !== 1" @click="openEdit(v)">Sửa</button>
                    <button class="btn tiny secondary" type="button" :disabled="workingId === v.key || videoSelectedChannelIds.length !== 1" @click="openAffiliatePicker(v)">Thêm link Affiliate</button>
                    <button class="btn tiny danger" type="button" :disabled="workingId === v.key || videoSelectedChannelIds.length !== 1" @click="deleteVideo(v.id)">Xoá</button>
                  </td>
                </tr>
              </tbody>
            </table>
          </div>

          <div v-else class="video-cards">
            <div v-for="v in videos" :key="v.key" class="video-card" :class="{ selected: selectedVideoIds.has(v.key) }">
              <div class="video-card-top">
                <label class="video-check">
                  <input type="checkbox" :checked="selectedVideoIds.has(v.key)" @change="toggleVideoSelection(v.key, ($event.target as HTMLInputElement).checked)" />
                </label>
                <div class="video-thumb-wrap">
                  <img v-if="v.thumbnailUrl" class="video-thumb" :src="v.thumbnailUrl" alt="thumb" />
                  <div v-else class="video-thumb placeholder" />
                </div>
              </div>

              <div class="video-card-body">
                <div class="video-title">{{ v.title }}</div>
                <div class="video-sub">{{ v.id }} • {{ v.channelId }}</div>

                <div class="video-meta">
                  <span :class="['badge', v.privacyStatus === 'public' ? 'ok' : 'warn']">{{ v.privacyStatus }}</span>
                  <span class="meta-dot">•</span>
                  <span class="meta-time">{{ formatDateTime(v.publishedAt) }}</span>
                </div>

                <div class="video-stats">
                  <div class="stat"><span class="stat-label">View</span><span class="stat-value">{{ formatCount(v.viewCount) }}</span></div>
                  <div class="stat"><span class="stat-label">Comment</span><span class="stat-value">{{ formatCount(v.commentCount) }}</span></div>
                  <div class="stat"><span class="stat-label">Like</span><span class="stat-value">{{ formatCount(v.likeCount) }}</span></div>
                  <div class="stat"><span class="stat-label">Unlike</span><span class="stat-value">{{ formatCount(v.dislikeCount) }}</span></div>
                </div>
              </div>

              <div class="video-card-actions">
                <button class="btn tiny" type="button" :disabled="workingId === v.key || videoSelectedChannelIds.length !== 1" @click="openEdit(v)">Sửa</button>
                <button class="btn tiny secondary" type="button" :disabled="workingId === v.key || videoSelectedChannelIds.length !== 1" @click="openAffiliatePicker(v)">Thêm link Affiliate</button>
                <button class="btn tiny danger" type="button" :disabled="workingId === v.key || videoSelectedChannelIds.length !== 1" @click="deleteVideo(v.id)">Xoá</button>
              </div>
            </div>
          </div>
        </div>
      </div>

      <div v-if="affiliatePickerOpen" class="modal-backdrop" @click.self="closeAffiliatePicker">
        <div class="modal affiliate-picker-modal card">
          <div class="modal-title">Chọn sản phẩm để thêm link Affiliate</div>

          <div class="picker-toolbar">
            <input v-model="affiliatePickerSearch" class="input" type="text" placeholder="Tìm theo ExternalItemId / tên..." :disabled="affiliatePickerLoading || affiliatePickerApplying" @keyup.enter="applyAffiliatePickerSearch" />
            <button class="btn secondary" type="button" :disabled="affiliatePickerLoading || affiliatePickerApplying" @click="applyAffiliatePickerSearch">Tìm</button>
          </div>

          <div v-if="affiliatePickerError" class="msg error">{{ affiliatePickerError }}</div>
          <div v-if="affiliatePickerLoading" class="muted">Đang tải...</div>

          <div v-else class="affiliate-picker-list">
            <button
              v-for="p in affiliatePickerItems"
              :key="p.externalItemId"
              type="button"
              class="affiliate-picker-item"
              :class="{ active: affiliatePickerSelected?.externalItemId === p.externalItemId }"
              @click="selectAffiliatePick(p)"
            >
              <img class="affiliate-picker-thumb" :src="p.imageUrl || '/placeholder-product.jpg'" alt="" />
              <div class="affiliate-picker-meta">
                <div class="affiliate-picker-name">{{ p.name }}</div>
                <div class="affiliate-picker-sub mono">{{ p.externalItemId }}</div>
                <div class="affiliate-picker-sub affiliate-picker-aff-link"><span class="muted">Affiliate:</span> {{ p.affiliateLink || '-' }}</div>
              </div>
            </button>
          </div>

          <div v-if="!affiliatePickerLoading" class="picker-paging">
            <div class="muted">Tổng: {{ affiliatePickerTotal }}</div>
            <div class="paging-actions">
              <button class="btn tiny secondary" type="button" :disabled="affiliatePickerLoading || affiliatePickerApplying || affiliatePickerPage <= 1" @click="goAffiliatePickerPage(affiliatePickerPage - 1)">Trước</button>
              <div class="mono">Trang {{ affiliatePickerPage }} / {{ affiliatePickerTotalPages }}</div>
              <button class="btn tiny secondary" type="button" :disabled="affiliatePickerLoading || affiliatePickerApplying || affiliatePickerPage >= affiliatePickerTotalPages" @click="goAffiliatePickerPage(affiliatePickerPage + 1)">Sau</button>
            </div>
          </div>

          <div class="actions">
            <button class="btn" type="button" :disabled="!affiliatePickerSelected || affiliatePickerApplying" @click="confirmAffiliatePicker">
              {{ affiliatePickerApplying ? 'Đang cập nhật...' : 'OK' }}
            </button>
            <button class="btn secondary" type="button" :disabled="affiliatePickerApplying" @click="closeAffiliatePicker">Đóng</button>
          </div>
        </div>
      </div>

      <div v-if="editOpen" class="modal-backdrop" @click.self="closeEdit">
        <div class="modal">
          <div class="modal-title">Chỉnh sửa video</div>

          <div class="form">
            <div class="field">
              <label>Title</label>
              <input v-model="editForm.title" class="input" type="text" />
            </div>
            <div class="field">
              <label>Description</label>
              <textarea v-model="editForm.description" class="textarea" rows="4" />
            </div>
            <div class="field">
              <label>Privacy</label>
              <select v-model="editForm.privacyStatus" class="input">
                <option value="public">public</option>
                <option value="unlisted">unlisted</option>
                <option value="private">private</option>
              </select>
            </div>

            <div class="field">
              <label>Tags (phân tách bằng dấu phẩy)</label>
              <input v-model="editForm.tagsText" class="input" type="text" placeholder="vd: girls, shorts, trending" />
            </div>

            <div class="field">
              <label>Thumbnail</label>
              <div class="thumb-actions">
                <input type="file" accept="image/*" @change="onPickThumb" />
                <button class="btn tiny" type="button" :disabled="savingThumb || !editForm.thumbFile" @click="saveThumb">
                  {{ savingThumb ? 'Đang cập nhật...' : 'Cập nhật thumbnail' }}
                </button>
              </div>
              <div v-if="editForm.thumbPreview" class="thumb-preview">
                <img class="thumb" :src="editForm.thumbPreview" alt="thumb-preview" />
              </div>
            </div>

            <div class="field">
              <label>Danh sách phát</label>
              <div v-if="playlistsLoading" class="muted">Đang tải playlist...</div>
              <div v-else-if="playlists.length === 0" class="muted">Không có playlist.</div>
              <div v-else class="playlist-list">
                <label v-for="p in playlists" :key="p.id" class="playlist-item">
                  <input
                    type="checkbox"
                    :checked="selectedPlaylistIds.has(p.id)"
                    :disabled="playlistWorkingId === p.id"
                    @change="togglePlaylist(p.id, ($event.target as HTMLInputElement).checked)"
                  />
                  <span class="playlist-title">{{ p.title }}</span>
                </label>
              </div>
            </div>

            <div class="actions">
              <button class="btn" type="button" :disabled="savingEdit" @click="saveEdit">
                {{ savingEdit ? 'Đang lưu...' : 'Lưu' }}
              </button>
              <button class="btn secondary" type="button" :disabled="savingEdit" @click="closeEdit">Đóng</button>
            </div>
          </div>
        </div>
      </div>

      <div v-if="bulkEditOpen" class="upload-overlay" @click.self="closeBulkEdit">
        <div class="upload-panel card bulk-upload-panel">
          <div class="sidebar-header">
            <div class="sidebar-title">Sửa hàng loạt ({{ selectedVideoIds.size }} video)</div>
            <button class="sidebar-close" type="button" @click="closeBulkEdit">✕</button>
          </div>

          <div class="sidebar-content">
            <div class="form">
              <div class="field">
                <label>Chọn kênh</label>
                <div class="row" style="gap: 8px; align-items: flex-end">
                  <div style="flex: 1; min-width: 260px">
                    <SelectMultiTags v-model="bulkEditSelectedChannelIds" :options="channelOptions" placeholder="Chọn kênh..." :disabled="bulkWorking" />
                  </div>
                </div>
                <div class="muted" style="margin-top: 6px">Danh sách video thuộc kênh nào thì sẽ tự chọn các kênh đó.</div>
              </div>

              <div class="field">
                <label>Thiết lập cập nhật</label>
                <div class="muted" style="margin-top: 6px">Bỏ trống trường nào thì sẽ không cập nhật trường đó.</div>
              </div>

              <div class="field">
                <label>SuffixTags</label>
                <div class="row" style="gap: 8px; align-items: center">
                  <label class="muted" style="display: inline-flex; gap: 6px; align-items: center">
                    <input type="checkbox" v-model="bulkEditApplySuffixTags" :disabled="bulkWorking" />
                    Áp dụng
                  </label>
                  <input
                    v-model="bulkEditSuffixTags"
                    class="input"
                    type="text"
                    placeholder="SuffixTags (append vào tags hiện tại)"
                    :disabled="bulkWorking || !bulkEditApplySuffixTags"
                    style="flex: 1"
                  />
                </div>
              </div>

              <div class="field">
                <label>SuffixTitle / SuffixDescription</label>
                <div class="row" style="gap: 8px; align-items: flex-end">
                  <div style="flex: 1; min-width: 240px">
                    <div class="row" style="gap: 8px; align-items: center">
                      <label class="muted" style="display: inline-flex; gap: 6px; align-items: center; white-space: nowrap">
                        <input type="checkbox" v-model="bulkEditApplySuffixTitle" :disabled="bulkWorking" />
                        Title
                      </label>
                      <input
                        v-model="bulkEditSuffixTitle"
                        class="input"
                        type="text"
                        placeholder="SuffixTitle (append)"
                        :disabled="bulkWorking || !bulkEditApplySuffixTitle"
                        style="flex: 1"
                      />
                    </div>
                  </div>
                  <textarea
                    v-model="bulkEditSuffixDescription"
                    class="textarea"
                    placeholder="SuffixDescription (append)"
                    :disabled="bulkWorking || !bulkEditApplySuffixDescription"
                    style="flex: 2; min-width: 320px"
                    rows="2"
                  />
                </div>
                <div class="row" style="gap: 8px; align-items: center; margin-top: 6px">
                  <label class="muted" style="display: inline-flex; gap: 6px; align-items: center">
                    <input type="checkbox" v-model="bulkEditApplySuffixDescription" :disabled="bulkWorking" />
                    Áp dụng SuffixDescription
                  </label>
                </div>
                <div class="muted" style="margin-top: 6px">Suffix sẽ được cộng thêm vào nội dung hiện tại của từng video.</div>
              </div>

              <div class="field">
                <label>Thiết lập công chiếu (UTC+7)</label>
                <div class="row" style="gap: 8px; align-items: flex-end">
                  <input v-model="bulkEditPremiereSchedule.startAtLocal" class="input" type="datetime-local" :disabled="bulkWorking" style="max-width: 220px" />
                  <input
                    v-model.number="bulkEditPremiereSchedule.intervalMinutes"
                    class="input"
                    type="number"
                    min="0"
                    step="1"
                    style="max-width: 140px"
                    :disabled="bulkWorking"
                  />
                  <button class="btn tiny" type="button" :disabled="bulkWorking" @click="() => applyBulkEditPremiereSchedule()">Áp dụng</button>
                </div>
                <div class="muted" style="margin-top: 6px">
                  Chỉ áp dụng cho các video đang chọn Privacy = premiere. Khi lưu, hệ thống sẽ update sang private + set publishAt.
                </div>
              </div>

              <div class="field">
                <label>Gen tiêu đề & mô tả (AI)</label>
                <div class="row" style="gap: 8px; align-items: flex-end">
                  <button class="btn tiny secondary" type="button" :disabled="bulkWorking" @click="bulkEditAiPromptOpen = true">
                    Tạo prompt
                  </button>
                  <button class="btn tiny secondary" type="button" :disabled="bulkWorking" @click="bulkEditAiImportOpen = !bulkEditAiImportOpen">
                    Import data
                  </button>
                </div>
                <div v-if="bulkEditAiImportOpen" class="row" style="gap: 8px; margin-top: 8px">
                  <textarea
                    v-model="bulkEditAiImportText"
                    class="textarea"
                    placeholder="Paste JSON từ AI vào đây..."
                    style="flex: 1; min-height: 80px"
                  />
                  <button class="btn tiny" type="button" :disabled="bulkWorking || !bulkEditAiImportText.trim()" @click="importBulkEditAiData">
                    Áp dụng
                  </button>
                </div>
              </div>

              <div v-if="selectedVideoIds.size === 0" class="muted">Chưa chọn video nào.</div>

              <div v-else class="bulk-grid-wrap">
                <div class="bulk-grid header">
                  <div class="cell select">
                    <input type="checkbox" checked disabled />
                  </div>
                  <div class="cell">Video</div>
                  <div class="cell">Trạng thái</div>
                  <div class="cell">Title</div>
                  <div class="cell">Description</div>
                  <div class="cell">Privacy</div>
                  <div class="cell">Tags</div>
                  <div class="cell">Xuất bản</div>
                </div>

                <div v-for="(v, idx) in videos.filter((x) => selectedVideoIds.has(x.key))" :key="v.key" class="bulk-grid row">
                  <div class="cell select">
                    <input type="checkbox" checked disabled />
                  </div>
                  <div class="cell file" :title="v.id"><span class="file-text">{{ idx + 1 }}. {{ v.id }}</span></div>
                  <div class="cell status">-</div>
                  <div class="cell">
                    <input v-model="v._editTitle" class="input tiny" type="text" :disabled="bulkWorking" style="width: 100%" />
                  </div>
                  <div class="cell">
                    <textarea v-model="v._editDescription" class="textarea tiny" rows="2" :disabled="bulkWorking" style="width: 100%; resize: vertical" />
                  </div>
                  <div class="cell">
                    <select v-model="v._editPrivacy" class="input tiny" :disabled="bulkWorking" style="width: 100%">
                      <option value="public">public</option>
                      <option value="unlisted">unlisted</option>
                      <option value="private">private</option>
                      <option value="premiere">premiere</option>
                    </select>
                  </div>
                  <div class="cell">
                    <input v-model="v._editTagsText" class="input tiny" type="text" :disabled="bulkWorking" style="width: 100%" placeholder="tags, separated, by, commas" />
                  </div>
                  <div class="cell">
                    <input v-model="v._editPublishAtLocal" class="input tiny" type="datetime-local" :disabled="bulkWorking || v._editPrivacy !== 'premiere'" />
                    <div v-if="v._editPrivacy !== 'premiere'" class="muted" style="margin-top: 4px">{{ formatDateTime(v.publishedAt) }}</div>
                  </div>
                </div>
              </div>

              <div class="actions" style="margin-top: 10px">
                <button class="btn" type="button" :disabled="bulkWorking || bulkEditSelectedChannelIds.length !== 1" @click="saveBulkEdit">
                  {{ bulkWorking ? 'Đang lưu...' : 'Lưu' }}
                </button>
                <button class="btn secondary" type="button" :disabled="bulkWorking" @click="closeBulkEdit">Đóng</button>
              </div>
            </div>
          </div>
        </div>
      </div>

      <div v-if="playlistManagerOpen" class="modal-backdrop" @click.self="closePlaylistManager">
        <div class="modal" style="max-width: 680px">
          <div class="modal-title">Quản lý Playlist</div>

          <div class="form">
            <div class="field">
              <label>Tạo playlist mới</label>
              <div class="row" style="gap: 8px">
                <input v-model="playlistCreateForm.title" class="input" type="text" placeholder="Title" />
                <select v-model="playlistCreateForm.privacyStatus" class="input" style="max-width: 160px">
                  <option value="public">public</option>
                  <option value="unlisted">unlisted</option>
                  <option value="private">private</option>
                </select>
              </div>
              <textarea v-model="playlistCreateForm.description" class="textarea" rows="2" placeholder="Description" style="margin-top: 8px" />
              <div class="actions" style="margin-top: 8px">
                <button class="btn tiny" type="button" :disabled="playlistManagerWorking" @click="createPlaylist">
                  {{ playlistManagerWorking ? '...' : 'Tạo playlist' }}
                </button>
              </div>
            </div>

            <div class="field">
              <label>Danh sách playlist</label>
              <div v-if="playlistsLoading" class="muted">Đang tải playlist...</div>
              <div v-else-if="playlists.length === 0" class="muted">Không có playlist.</div>
              <div v-else class="playlist-manager-list">
                <div v-for="p in playlists" :key="p.id" class="playlist-manager-item">
                  <div class="playlist-manager-main">
                    <div class="playlist-manager-title">{{ p.title }}</div>
                    <div class="playlist-manager-sub">{{ p.id }}</div>
                  </div>
                  <div class="playlist-manager-actions">
                    <button class="btn tiny secondary" type="button" :disabled="playlistManagerWorking" @click="openPlaylistEdit(p)">Sửa</button>
                    <button class="btn tiny danger" type="button" :disabled="playlistManagerWorking" @click="deletePlaylist(p.id)">Xoá</button>
                  </div>
                </div>
              </div>
            </div>

            <div class="actions">
              <button class="btn secondary" type="button" :disabled="playlistManagerWorking" @click="closePlaylistManager">Đóng</button>
            </div>
          </div>
        </div>
      </div>

      <div v-if="playlistEditOpen" class="modal-backdrop" @click.self="closePlaylistEdit">
        <div class="modal">
          <div class="modal-title">Sửa Playlist</div>

          <div class="form">
            <div class="field">
              <label>Title</label>
              <input v-model="playlistEditForm.title" class="input" type="text" />
            </div>
            <div class="field">
              <label>Description</label>
              <textarea v-model="playlistEditForm.description" class="textarea" rows="3" />
            </div>
            <div class="field">
              <label>Privacy</label>
              <select v-model="playlistEditForm.privacyStatus" class="input">
                <option value="public">public</option>
                <option value="unlisted">unlisted</option>
                <option value="private">private</option>
              </select>
            </div>

            <div class="actions">
              <button class="btn" type="button" :disabled="playlistManagerWorking" @click="savePlaylistEdit">
                {{ playlistManagerWorking ? '...' : 'Lưu' }}
              </button>
              <button class="btn secondary" type="button" :disabled="playlistManagerWorking" @click="closePlaylistEdit">Đóng</button>
            </div>
          </div>
        </div>
      </div>

      <div v-else-if="activeTab === 'analytics'" class="card tab-main">
        <div class="card-title">Analytics</div>

        <div class="muted" style="margin-bottom: 10px">
          Các số liệu sẽ lấy từ YouTube Analytics API (watch time, retention, traffic source...).
        </div>

        <div class="analytics-grid">
          <div class="metric">
            <div class="metric-label">Watch time (minutes)</div>
            <div class="metric-value">{{ analytics.watchTimeMinutes ?? '-' }}</div>
          </div>
          <div class="metric">
            <div class="metric-label">Views</div>
            <div class="metric-value">{{ analytics.views ?? '-' }}</div>
          </div>
          <div class="metric">
            <div class="metric-label">Average view duration (sec)</div>
            <div class="metric-value">{{ analytics.avgViewDurationSec ?? '-' }}</div>
          </div>
          <div class="metric">
            <div class="metric-label">Subscribers gained</div>
            <div class="metric-value">{{ analytics.subsGained ?? '-' }}</div>
          </div>
        </div>

        <div class="actions" style="margin-top: 12px">
          <button class="btn" type="button" :disabled="analyticsLoading" @click="loadAnalytics">
            {{ analyticsLoading ? 'Đang tải...' : 'Tải Analytics' }}
          </button>
        </div>
      </div>

      <div v-if="uploadOpen" class="upload-overlay" @click.self="closeUpload">
        <div class="upload-panel card">
          <div class="sidebar-header">
            <div class="sidebar-title">Upload video</div>
            <button class="sidebar-close" type="button" @click="closeUpload">✕</button>
          </div>

          <div class="sidebar-content">
            <div class="form">
              <div class="field">
                <label>Chọn kênh upload</label>
                <SelectMultiTags v-model="uploadSelectedChannelIds" :options="channelOptions" placeholder="Chọn kênh..." :disabled="uploading || loading" />
              </div>

              <div class="field">
                <label>File video</label>
                <input type="file" accept="video/*" @change="onPickFile" />
                <div v-if="uploadFile" class="muted" style="margin-top: 6px">{{ uploadFile.name }}</div>
              </div>

              <div class="field">
                <label>Đặt lịch xuất bản (UTC+7)</label>
                <input v-model="uploadForm.publishAtLocal" class="input" type="datetime-local" :disabled="uploading || uploadForm.uploadStatus !== 'premiere'" />
                <div v-if="uploadForm.uploadStatus === 'premiere' && uploadForm.publishAtLocal" class="muted" style="margin-top: 6px">
                  Sẽ đặt lịch xuất bản (công chiếu), hệ thống sẽ upload ở trạng thái private.
                </div>
              </div>

              <div class="field">
                <label class="label-row">
                  <span>Title</span>
                  <span class="char-counter">{{ (uploadForm.title ?? '').trim().length }}/100</span>
                </label>
                <input v-model="uploadForm.title" class="input" type="text" placeholder="Nhập title..." />
              </div>

              <div class="field">
                <label class="label-row">
                  <span>Description</span>
                  <span class="char-counter">{{ (uploadForm.description ?? '').trim().length }}/5000</span>
                </label>
                <textarea v-model="uploadForm.description" class="textarea" rows="4" placeholder="Nhập mô tả..." />
              </div>

              <div class="field">
                <label>Trạng thái</label>
                <select v-model="uploadForm.uploadStatus" class="input">
                  <option value="private">Private - không đặt lịch</option>
                  <option value="premiere">Công chiếu - có đặt lịch</option>
                  <option value="public">Công khai - không đặt lịch</option>
                </select>
              </div>

              <div class="field">
                <label>Tags (phân tách bằng dấu phẩy)</label>
                <input v-model="uploadForm.tagsText" class="input" type="text" placeholder="vd: shorts, trending" />
              </div>

              <div class="actions" style="margin-top: 10px">
                <button class="btn secondary" type="button" :disabled="uploading || savingDefaults" @click="saveSelectedChannelDefaults">
                  {{ savingDefaults ? 'Đang lưu...' : 'Lưu mặc định' }}
                </button>
              </div>

              <div class="field">
                <label>Playlist</label>
                <div v-if="uploadSelectedChannelIds.length !== 1" class="muted">Chọn đúng 1 kênh để chọn playlist.</div>
                <div v-else-if="playlistsLoading" class="muted">Đang tải playlist...</div>
                <div v-else-if="playlists.length === 0" class="muted">Không có playlist.</div>
                <div v-else class="playlist-list">
                  <label v-for="p in playlists" :key="p.id" class="playlist-item">
                    <input
                      type="checkbox"
                      :checked="uploadSelectedPlaylistIds.has(p.id)"
                      :disabled="uploading || uploadSelectedChannelIds.length !== 1"
                      @change="toggleUploadPlaylist(p.id, ($event.target as HTMLInputElement).checked)"
                    />
                    <span class="playlist-title">{{ p.title }}</span>
                  </label>
                </div>
              </div>

              <div v-if="uploading" class="field">
                <label>Tiến trình</label>
                <div class="progress">
                  <div class="bar" :style="{ width: uploadProgress + '%' }" />
                </div>
                <div class="muted" style="margin-top: 6px">{{ uploadProgress }}%</div>
              </div>

              <div class="actions">
                <button class="btn" type="button" :disabled="uploading || uploadSelectedChannelIds.length === 0" @click="uploadVideo">
                  {{ uploading ? 'Đang upload...' : 'Upload' }}
                </button>
                <button class="btn secondary" type="button" :disabled="uploading" @click="closeUpload">Đóng</button>
              </div>
            </div>
          </div>
        </div>
      </div>

      <div v-if="bulkUploadOpen" class="upload-overlay" @click.self="closeBulkUpload">
        <div class="upload-panel card bulk-upload-panel">
          <div class="sidebar-header">
            <div class="sidebar-title">Upload hàng loạt</div>
            <button class="sidebar-close" type="button" @click="closeBulkUpload">✕</button>
          </div>

          <div class="sidebar-content">
            <div class="form">
              <div class="field">
                <label>Chọn kênh & File</label>
                <div class="row" style="gap: 8px; align-items: flex-end">
                  <div style="flex: 1; min-width: 260px">
                    <SelectMultiTags v-model="uploadSelectedChannelIds" :options="channelOptions" placeholder="Chọn kênh..." :disabled="bulkUploading || loading" />
                  </div>
                  <input type="file" accept="video/*" multiple @change="onPickBulkFiles" :disabled="bulkUploading" style="min-width: 260px" />
                </div>
              </div>

              <div class="field">
                <label>Thiết lập công chiếu & SuffixTags</label>
                <div class="row" style="gap: 8px; align-items: flex-end">
                  <input v-model="bulkPremiereSchedule.startAtLocal" class="input" type="datetime-local" :disabled="bulkUploading" style="max-width: 220px" />
                  <input
                    v-model.number="bulkPremiereSchedule.intervalMinutes"
                    class="input"
                    type="number"
                    min="0"
                    step="1"
                    style="max-width: 140px"
                    :disabled="bulkUploading"
                  />
                  <button class="btn tiny" type="button" :disabled="bulkUploading" @click="applyBulkPremiereSchedule">
                    Áp dụng
                  </button>
                  <input
                    v-model="bulkSuffixTags"
                    class="input"
                    type="text"
                    placeholder="SuffixTags (áp dụng toàn bộ danh sách)"
                    :disabled="bulkUploading"
                    style="flex: 1; min-width: 240px"
                  />
                </div>
                <div class="muted" style="margin-top: 6px">
                  Video đầu tiên mặc định Công khai. Các video Công chiếu sẽ tự động tăng theo khoảng cách (phút) kể từ thời gian bắt đầu.
                </div>
              </div>

              <div class="field">
                <label>SuffixTitle / SuffixDescription</label>
                <div class="row" style="gap: 8px; align-items: flex-end">
                  <input v-model="bulkSuffixTitle" class="input" type="text" placeholder="SuffixTitle" :disabled="bulkUploading" style="flex: 1; min-width: 240px" />
                  <textarea
                    v-model="bulkSuffixDescription"
                    class="textarea"
                    placeholder="SuffixDescription"
                    :disabled="bulkUploading"
                    style="flex: 2; min-width: 320px"
                    rows="2"
                  />
                </div>
              </div>

              <div v-if="bulkUploadItems.length === 0" class="muted">Chưa có file nào.</div>

              <div v-else class="bulk-grid-wrap">
                <div class="bulk-grid header">
                  <div class="cell select">
                    <input type="checkbox" :checked="bulkAllSelected" :disabled="bulkUploading" @change="toggleBulkSelectAll(($event.target as HTMLInputElement).checked)" />
                  </div>
                  <div class="cell">File</div>
                  <div class="cell">Trạng thái</div>
                  <div class="cell">Title</div>
                  <div class="cell">Description</div>
                  <div class="cell">Trạng thái upload</div>
                  <div class="cell">Tags</div>
                  <div class="cell">Lịch (UTC+7)</div>
                </div>

                <div v-for="(it, idx) in bulkUploadItems" :key="it.key" class="bulk-grid row">
                  <div class="cell select">
                    <input type="checkbox" :checked="bulkSelectedKeys.has(it.key)" :disabled="bulkUploading" @change="toggleBulkSelectOne(it.key, ($event.target as HTMLInputElement).checked)" />
                  </div>
                  <div class="cell file" :title="it.file.name"><span class="file-text">{{ idx + 1 }}. {{ it.file.name }}</span></div>
                  <div class="cell status" :class="it.status">{{ it.statusLabel }}</div>

                  <div class="cell">
                    <div class="input-wrap">
                      <input v-model="it.title" class="input" type="text" placeholder="Title" :disabled="bulkUploading" />
                      <div class="char-counter">{{ (it.title ?? '').trim().length }}/{{ bulkBaseTitleMaxLen }}</div>
                    </div>
                  </div>

                  <div class="cell">
                    <div class="input-wrap">
                      <textarea v-model="it.description" class="textarea" rows="2" placeholder="Description" :disabled="bulkUploading" />
                      <div class="char-counter">{{ (it.description ?? '').trim().length }}/5000</div>
                    </div>
                  </div>

                  <div class="cell">
                    <select v-model="it.uploadStatus" class="input" :disabled="bulkUploading">
                      <option value="private">Private</option>
                      <option value="premiere">Công chiếu</option>
                      <option value="public">Công khai</option>
                    </select>
                  </div>

                  <div class="cell">
                    <div class="muted" style="white-space: nowrap; overflow: hidden; text-overflow: ellipsis" :title="bulkSuffixTags">
                      {{ bulkSuffixTags || '-' }}
                    </div>
                  </div>

                  <div class="cell">
                    <input v-model="it.publishAtLocal" class="input" type="datetime-local" :disabled="bulkUploading || it.uploadStatus !== 'premiere'" />
                  </div>
                </div>
              </div>

              <div v-if="bulkUploading" class="field" style="margin-top: 10px">
                <label>Tiến trình</label>
                <div class="progress">
                  <div class="bar" :style="{ width: bulkProgress + '%' }" />
                </div>
                <div class="muted" style="margin-top: 6px">{{ bulkProgress }}%</div>
              </div>

              <div class="actions" style="margin-top: 10px">
                <button class="btn secondary" type="button" :disabled="bulkUploading || bulkSelectedKeys.size === 0" @click="openBulkAiPrompt">
                  Gen tiêu đề & mô tả (AI)
                </button>
                <button class="btn" type="button" :disabled="bulkUploading || uploadSelectedChannelIds.length === 0 || bulkUploadItems.length === 0" @click="uploadBulkVideos">
                  {{ bulkUploading ? 'Đang upload...' : 'Bắt đầu upload hàng loạt' }}
                </button>
                <button class="btn secondary" type="button" :disabled="bulkUploading" @click="clearBulkUpload">
                  Xoá danh sách
                </button>
                <button class="btn secondary" type="button" :disabled="bulkUploading" @click="closeBulkUpload">
                  Đóng
                </button>
              </div>
            </div>
          </div>
        </div>
      </div>

      <div v-if="bulkAiPromptOpen" class="modal-backdrop" @click.self="closeBulkAiPrompt">
        <div class="modal" style="max-width: 860px">
          <div class="modal-title">Tạo prompt Gen tiêu đề & mô tả</div>

          <div class="muted" style="margin-top: 6px">
            Sẽ tạo prompt cho <b>{{ bulkSelectedKeys.size }}</b> video đã chọn.
          </div>

          <div class="form" style="margin-top: 10px">
            <div class="field">
              <label>Tags / Chủ đề (phân tách bằng dấu phẩy)</label>
              <input v-model="bulkAiPromptForm.tagsText" class="input" type="text" placeholder="vd: tài chính, kiếm tiền online, affiliate" />
            </div>

            <div class="field">
              <label>Bối cảnh / yêu cầu thêm</label>
              <textarea v-model="bulkAiPromptForm.context" class="textarea" rows="5" placeholder="Nhập bối cảnh..." />
            </div>

            <div class="field">
              <label>Prompt đã tạo</label>
              <textarea v-model="bulkAiPromptText" class="textarea" rows="10" placeholder="Nhấn Tạo prompt để sinh nội dung..." readonly />
            </div>

            <div class="field">
              <label>Import data (paste JSON AI trả về)</label>
              <textarea v-model="bulkAiImportText" class="textarea" rows="8" placeholder='Dán JSON array: [{"title":"...","description":"..."}, ...] hoặc {"items":[...]}' />
              <div class="muted" style="margin-top: 6px">
                Import sẽ fill vào các video đã chọn theo đúng thứ tự checkbox (không tự cộng suffix, suffix sẽ được áp dụng khi upload).
              </div>
            </div>

            <div class="actions" style="margin-top: 10px">
              <button class="btn" type="button" :disabled="bulkSelectedKeys.size === 0" @click="buildBulkAiPrompt">
                Tạo prompt
              </button>
              <button class="btn secondary" type="button" :disabled="!bulkAiPromptText" @click="copyBulkAiPrompt">
                Copy
              </button>
              <button class="btn secondary" type="button" :disabled="bulkSelectedKeys.size === 0 || !bulkAiImportText" @click="importBulkAiData">
                Import data
              </button>
              <button class="btn secondary" type="button" @click="closeBulkAiPrompt">Đóng</button>
            </div>
          </div>
        </div>
      </div>

      <div v-if="scanLinksOpen" class="modal-backdrop" @click.self="closeScanLinks">
        <div class="modal scan-links-modal">
          <div class="modal-title">Rà soát link trong mô tả</div>

          <div class="scan-toolbar">
            <div class="field" style="min-width: 240px; max-width: 420px">
              <label>Lọc query (tuỳ chọn)</label>
              <input v-model="scanLinksQuery" class="input" type="text" placeholder="vd: review, shorts..." @keyup.enter="scanLinks" />
            </div>
            <div class="field" style="max-width: 220px">
              <label>Giới hạn video/kênh</label>
              <input v-model.number="scanLinksLimit" class="input" type="number" min="1" max="500" />
            </div>

            <div class="scan-actions">
              <button class="btn" type="button" :disabled="scanLinksLoading" @click="scanLinks">
                {{ scanLinksLoading ? 'Đang rà soát...' : 'Rà soát' }}
              </button>
              <button class="btn secondary" type="button" :disabled="scanLinksLoading" @click="toggleScanSelectAll(!scanAllSelected)">
                {{ scanAllSelected ? 'Bỏ chọn tất cả' : 'Chọn tất cả' }}
              </button>
              <button class="btn" type="button" :disabled="collectLinksLoading || scanCollectableExistingCount === 0" @click="collectLinks">
                {{ collectLinksLoading ? 'Đang thu thập...' : 'Thu thập' }}
              </button>
              <button class="btn secondary" type="button" :disabled="scanLinksLoading" @click="closeScanLinks">Đóng</button>
            </div>
          </div>

          <div v-if="scanLinksError" class="msg error" style="margin-top: 10px">{{ scanLinksError }}</div>
          <div v-if="scanLinksInfo" class="msg success" style="margin-top: 10px">{{ scanLinksInfo }}</div>

          <div class="muted" style="margin-top: 10px">
            Tìm thấy: {{ scanLinksRows.length }} • Đã chọn: {{ scanSelectedKeys.size }}
          </div>

          <div class="table-wrap" style="margin-top: 10px">
            <table class="table">
              <thead>
                <tr>
                  <th style="width: 42px">
                    <input type="checkbox" :checked="scanAllSelected" :disabled="scanLinksRows.length === 0" @change="toggleScanSelectAll(($event.target as HTMLInputElement).checked)" />
                  </th>
                  <th style="width: 220px">Video</th>
                  <th>ShortLink</th>
                  <th style="width: 140px">Trạng thái</th>
                  <th style="width: 140px">Actions</th>
                </tr>
              </thead>
              <tbody>
                <tr v-for="r in scanLinksRows" :key="r.key" :class="{ selected: scanSelectedKeys.has(r.key) }">
                  <td>
                    <input type="checkbox" :checked="scanSelectedKeys.has(r.key)" @change="toggleScanSelection(r.key, ($event.target as HTMLInputElement).checked)" />
                  </td>
                  <td>
                    <div class="scan-video">
                      <div class="scan-video-title">{{ r.videoTitle }}</div>
                      <div class="scan-video-sub">{{ r.videoId }} • {{ r.channelId }}</div>
                    </div>
                  </td>
                  <td class="mono">
                    <a :href="r.shortLink" target="_blank" rel="noreferrer">{{ r.shortLink }}</a>
                  </td>
                  <td>
                    <span :class="['badge', r.existsInDb ? 'ok' : 'warn']">{{ r.existsInDb ? 'Đã có' : 'Chưa có' }}</span>
                  </td>
                  <td>
                    <button
                      v-if="!r.existsInDb"
                      class="btn"
                      type="button"
                      :disabled="scanCreateLoadingKeys.has(r.key)"
                      @click="createAffiliateData(r)"
                    >
                      {{ scanCreateLoadingKeys.has(r.key) ? 'Đang tạo...' : 'Tạo dữ liệu' }}
                    </button>
                  </td>
                </tr>
              </tbody>
            </table>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, reactive, ref, watch } from 'vue'
import api from '@/infrastructure/http/apiClient'
import SelectMultiTags from '@/shared/ui/SelectMultiTags.vue'

type YoutubeChannel = {
  id: string
  title: string
  thumbnailUrl?: string | null
  isAuthorized: boolean
  updatedAt?: string | null
}

type YoutubeVideo = {
  id: string
  title: string
  description?: string | null
  thumbnailUrl?: string | null
  publishedAt?: string | null
  privacyStatus: string
  viewCount?: number | null
  commentCount?: number | null
  likeCount?: number | null
  dislikeCount?: number | null
  tags?: string[] | null
}

type YoutubeVideoRow = YoutubeVideo & {
  channelId: string
  key: string

  _editTitle?: string
  _editDescription?: string
  _editPrivacy?: string
  _editTagsText?: string
  _editPublishAtLocal?: string

  _origTitle?: string
  _origDescription?: string
}

type YoutubeAnalyticsSummary = {
  watchTimeMinutes?: number | null
  views?: number | null
  avgViewDurationSec?: number | null
  subsGained?: number | null
}

type ScanSocialLinkRow = {
  channelId: string
  videoId: string
  videoTitle: string
  publishedAt?: string | null
  productLink: string
  affiliateLink: string
  shortLink: string
  socialLink: string
  existsInDb: boolean
  key: string
}

type AffiliatePickerItem = {
  externalItemId: string
  name: string
  imageUrl?: string | null
  affiliateLink?: string | null
  hasSocialLinks: boolean
}

type AffiliatePickerResponse = {
  items: AffiliatePickerItem[]
  total: number
  page: number
  pageSize: number
}

const activeTab = ref<'channels' | 'videos' | 'analytics'>('channels')

const loading = ref(false)
const connecting = ref(false)
const workingId = ref<string | null>(null)

const message = ref('')
const messageType = ref<'success' | 'error'>('success')

const channels = ref<YoutubeChannel[]>([])
const videoSelectedChannelIds = ref<string[]>([])

const videoLoading = ref(false)
const videos = ref<YoutubeVideoRow[]>([])
const videoQuery = ref('')
const videoViewMode = ref<'cards' | 'list'>('cards')

const selectedVideoIds = ref<Set<string>>(new Set())

const affiliatePickerOpen = ref(false)
const affiliatePickerLoading = ref(false)
const affiliatePickerApplying = ref(false)
const affiliatePickerError = ref('')
const affiliatePickerItems = ref<AffiliatePickerItem[]>([])
const affiliatePickerSelected = ref<AffiliatePickerItem | null>(null)
const affiliatePickerTargetVideo = ref<YoutubeVideoRow | null>(null)
const affiliatePickerSearch = ref('')
const affiliatePickerAppliedSearch = ref('')
const affiliatePickerPage = ref(1)
const affiliatePickerPageSize = ref(20)
const affiliatePickerTotal = ref(0)

const affiliatePickerTotalPages = computed(() => {
  const total = Math.max(0, affiliatePickerTotal.value)
  const size = Math.max(1, affiliatePickerPageSize.value)
  return Math.max(1, Math.ceil(total / size))
})

const videoAllSelected = computed(() => {
  if ((videos.value ?? []).length === 0) return false
  for (const v of videos.value) {
    if (!selectedVideoIds.value.has(v.key)) return false
  }
  return true
})

const toggleVideoSelectAll = (checked: boolean) => {
  if (!checked) {
    selectedVideoIds.value = new Set()
    return
  }
  const next = new Set<string>()
  for (const v of videos.value ?? []) {
    next.add(v.key)
  }
  selectedVideoIds.value = next
}

const bulkEditSelectedChannelIds = ref<string[]>([])
const bulkPlaylistId = ref('')
const bulkWorking = ref(false)
const bulkEditOpen = ref(false)
const bulkEditForm = reactive({
  privacyStatus: '',
})

const bulkEditSuffixTags = ref('')
const bulkEditSuffixTitle = ref('')
const bulkEditSuffixDescription = ref('')

const bulkEditApplySuffixTags = ref(false)
const bulkEditApplySuffixTitle = ref(false)
const bulkEditApplySuffixDescription = ref(false)

const bulkEditPremiereSchedule = reactive({
  startAtLocal: '',
  intervalMinutes: 10,
})

const bulkEditSuffixLastApplied = reactive<{ channelId: string; tags: string; title: string; description: string }>({
  channelId: '',
  tags: '',
  title: '',
  description: '',
})

const bulkEditAiPromptOpen = ref(false)
const bulkEditAiImportOpen = ref(false)
const bulkEditAiImportText = ref('')

const playlistManagerOpen = ref(false)
const playlistManagerWorking = ref(false)
const playlistEditOpen = ref(false)
const playlistCreateForm = reactive({
  title: '',
  description: '',
  privacyStatus: 'public',
})
const playlistEditForm = reactive({
  id: '',
  title: '',
  description: '',
  privacyStatus: 'public',
})

const scanLinksOpen = ref(false)
const scanLinksLoading = ref(false)
const collectLinksLoading = ref(false)
const scanLinksQuery = ref('')
const scanLinksLimit = ref(200)
const scanLinksRows = ref<ScanSocialLinkRow[]>([])
const scanSelectedKeys = ref<Set<string>>(new Set())
const scanCreateLoadingKeys = ref<Set<string>>(new Set())
const scanLinksError = ref('')
const scanLinksInfo = ref('')

const scanAllSelected = computed(() => {
  if ((scanLinksRows.value ?? []).length === 0) return false
  for (const r of scanLinksRows.value) {
    if (!scanSelectedKeys.value.has(r.key)) return false
  }
  return true
})

const scanCollectableExistingCount = computed(() => {
  const existing = (scanLinksRows.value ?? []).filter((x) => x.existsInDb)
  return existing.length
})

const toggleScanSelectAll = (checked: boolean) => {
  if (!checked) {
    scanSelectedKeys.value = new Set()
    return
  }
  const next = new Set<string>()
  for (const r of scanLinksRows.value ?? []) {
    next.add(r.key)
  }
  scanSelectedKeys.value = next
}

const toggleScanSelection = (key: string, checked: boolean) => {
  const next = new Set<string>(scanSelectedKeys.value)
  if (!checked) next.delete(key)
  else next.add(key)
  scanSelectedKeys.value = next
}

const openScanLinks = () => {
  scanLinksError.value = ''
  scanLinksInfo.value = ''
  scanLinksOpen.value = true
}

const closeScanLinks = () => {
  if (scanLinksLoading.value || collectLinksLoading.value) return
  scanLinksOpen.value = false
}

const scanLinks = async () => {
  scanLinksError.value = ''
  scanLinksInfo.value = ''
  scanLinksLoading.value = true
  try {
    const data = await api.post<any[]>('admin/youtube-social-links/scan', {
      query: scanLinksQuery.value.trim() || null,
      videoLimitPerChannel: Math.max(1, Math.min(Number(scanLinksLimit.value || 200), 500)),
    })

    const list = Array.isArray(data) ? data : []
    const rows: ScanSocialLinkRow[] = []
    for (const x of list) {
      const channelId = (x?.channelId ?? '').toString()
      const videoId = (x?.videoId ?? '').toString()
      const productLink = (x?.productLink ?? '').toString()
      const affiliateLink = (x?.affiliateLink ?? '').toString()
      const shortLink = ((x?.shortLink ?? '') || (x?.affiliateLink ?? '')).toString()
      const socialLink = (x?.socialLink ?? '').toString()
      if (!channelId || !videoId || !shortLink) continue
      rows.push({
        channelId,
        videoId,
        videoTitle: (x?.videoTitle ?? '').toString(),
        publishedAt: (x?.publishedAt ?? null) as any,
        productLink,
        affiliateLink,
        shortLink,
        socialLink,
        existsInDb: Boolean(x?.existsInDb),
        key: `${channelId}:${videoId}:${shortLink}`,
      })
    }

    scanLinksRows.value = rows
    scanSelectedKeys.value = new Set()
    scanLinksInfo.value = `Đã rà soát xong. Tìm thấy ${rows.length} link.`
  } catch (e: any) {
    scanLinksError.value = e?.message ?? 'Không rà soát được link.'
  } finally {
    scanLinksLoading.value = false
  }
}

const collectLinks = async () => {
  scanLinksError.value = ''
  scanLinksInfo.value = ''
  const existing = (scanLinksRows.value ?? []).filter((x) => x.existsInDb)
  if (existing.length === 0) return

  collectLinksLoading.value = true
  try {
    const payload = existing.map((x) => ({
      shortLink: x.shortLink,
      socialLink: x.socialLink,
    }))

    const res = await api.post<{ success: boolean; inserted: number; missing: number }>('admin/youtube-social-links/collect-existing', {
      items: payload,
    })

    scanLinksInfo.value = `Thu thập xong. Insert mới: ${res?.inserted ?? 0}${(res as any)?.missing ? ` • Missing mapping: ${(res as any)?.missing}` : ''}`
  } catch (e: any) {
    scanLinksError.value = e?.message ?? 'Không thu thập được.'
  } finally {
    collectLinksLoading.value = false
  }
}

const createAffiliateData = async (row: ScanSocialLinkRow) => {
  scanLinksError.value = ''
  scanLinksInfo.value = ''
  if (!row?.shortLink) return

  const next = new Set<string>(scanCreateLoadingKeys.value)
  next.add(row.key)
  scanCreateLoadingKeys.value = next

  try {
    const res = await api.post<{ success: boolean; externalItemId: string; productLink: string; affiliateLink: string }>(
      'admin/youtube-social-links/create-affiliate-link',
      {
        shortLink: row.shortLink,
      }
    )

    const productLink = (res as any)?.productLink ?? ''
    scanLinksRows.value = (scanLinksRows.value ?? []).map((r) => (r.key === row.key ? { ...r, productLink, existsInDb: true } : r))
    scanLinksInfo.value = 'Tạo dữ liệu thành công.'
  } catch (e: any) {
    scanLinksError.value = e?.message ?? 'Không tạo được dữ liệu.'
  } finally {
    const done = new Set<string>(scanCreateLoadingKeys.value)
    done.delete(row.key)
    scanCreateLoadingKeys.value = done
  }
}


const editOpen = ref(false)
const savingEdit = ref(false)
const savingThumb = ref(false)
const playlistsLoading = ref(false)
const playlistWorkingId = ref<string | null>(null)
const playlists = ref<{ id: string; title: string }[]>([])
const selectedPlaylistIds = ref<Set<string>>(new Set())
const editForm = reactive({
  id: '',
  title: '',
  description: '',
  privacyStatus: 'public',
  tagsText: '',
  thumbFile: null as File | null,
  thumbPreview: '' as string,
})

const uploadOpen = ref(false)
const bulkUploadOpen = ref(false)
const uploading = ref(false)
const uploadProgress = ref(0)
const uploadFile = ref<File | null>(null)
const uploadForm = reactive({
  title: '',
  description: '',
  uploadStatus: 'public' as 'private' | 'premiere' | 'public',
  tagsText: '',
  publishAtLocal: '',
})

type BulkUploadItem = {
  key: string
  file: File
  title: string
  description: string
  uploadStatus: 'private' | 'premiere' | 'public'
  tagsText: string
  publishAtLocal: string
  status: 'pending' | 'uploading' | 'success' | 'error'
  statusLabel: string
}

const bulkUploadItems = reactive<BulkUploadItem[]>([])
const bulkUploading = ref(false)
const bulkProgress = ref(0)

const bulkSelectedKeys = ref<Set<string>>(new Set())

const bulkAllSelected = computed(() => {
  if (bulkUploadItems.length === 0) return false
  for (const it of bulkUploadItems) {
    if (!bulkSelectedKeys.value.has(it.key)) return false
  }
  return true
})

const toggleBulkSelectOne = (key: string, checked: boolean) => {
  if (!key) return
  if (checked) {
    bulkSelectedKeys.value.add(key)
  } else {
    bulkSelectedKeys.value.delete(key)
  }
  bulkSelectedKeys.value = new Set(bulkSelectedKeys.value)
}

const toggleBulkSelectAll = (checked: boolean) => {
  if (!checked) {
    bulkSelectedKeys.value = new Set()
    return
  }
  const next = new Set<string>()
  for (const it of bulkUploadItems) {
    next.add(it.key)
  }
  bulkSelectedKeys.value = next
}

const bulkAiPromptOpen = ref(false)
const bulkAiPromptForm = reactive({
  tagsText: '',
  context: '',
})
const bulkAiPromptText = ref('')
const bulkAiImportText = ref('')

const openBulkAiPrompt = () => {
  bulkAiPromptForm.tagsText = (bulkSuffixTags.value ?? '').toString()
  bulkAiPromptForm.context = ''
  bulkAiPromptText.value = ''
  bulkAiImportText.value = ''
  bulkAiPromptOpen.value = true
}

const closeBulkAiPrompt = () => {
  bulkAiPromptOpen.value = false
}

const buildBulkAiPrompt = () => {
  const selected = bulkUploadItems.filter((x) => bulkSelectedKeys.value.has(x.key))
  const n = selected.length
  if (n === 0) {
    bulkAiPromptText.value = ''
    return
  }

  const tags = (bulkAiPromptForm.tagsText ?? '').toString().trim()
  const ctx = (bulkAiPromptForm.context ?? '').toString().trim()
  const suffixTitle = (bulkSuffixTitle.value ?? '').toString()
  const suffixDescription = (bulkSuffixDescription.value ?? '').toString()
  const maxBaseTitleLen = bulkBaseTitleMaxLen.value
  const maxBaseDescLen = Math.max(0, 5000 - suffixDescription.length)

  const parts: string[] = []
  parts.push('Bạn là chuyên gia viết tiêu đề & mô tả YouTube (tiếng Việt), ưu tiên HOOK mạnh, CTR cao, không vi phạm chính sách.')
  parts.push('YÊU CẦU:')
  parts.push(`- Tạo ${n} cặp {title, description} khác nhau.`)
  if (tags) parts.push(`- Chủ đề/tags chính: ${tags}`)
  if (ctx) parts.push(`- Bối cảnh/yêu cầu thêm: ${ctx}`)
  parts.push('- Output CHỈ trả về JSON hợp lệ (không markdown, không giải thích).')
  parts.push('- Format output: một JSON array, mỗi phần tử có đúng 2 key: title, description.')
  parts.push(`- Giới hạn: title <= ${maxBaseTitleLen} ký tự (chỉ phần title gốc, vì hệ thống sẽ tự cộng suffixTitle sau).`)
  parts.push(`- Giới hạn: description <= ${maxBaseDescLen} ký tự (chỉ phần mô tả gốc, vì hệ thống sẽ tự cộng suffixDescription sau).`)
  if (suffixTitle.trim()) parts.push(`- suffixTitle sẽ được cộng sau title: ${JSON.stringify(suffixTitle)}`)
  if (suffixDescription.trim()) parts.push(`- suffixDescription sẽ được cộng sau description: ${JSON.stringify(suffixDescription)}`)
  parts.push('- Tránh trùng lặp ý tưởng giữa các title.')
  parts.push('- Title nên ngắn gọn, có tò mò, có số liệu/đối lập/hứa hẹn rõ ràng (nhưng không clickbait quá đà).')
  parts.push('BẮT ĐẦU TRẢ KẾT QUẢ JSON:')

  bulkAiPromptText.value = parts.join('\n')
}

const copyBulkAiPrompt = async () => {
  const t = (bulkAiPromptText.value ?? '').toString()
  if (!t.trim()) return
  try {
    await navigator.clipboard.writeText(t)
    setMsg('Đã copy prompt.', 'success')
  } catch (e: any) {
    setMsg(e?.message ?? 'Không copy được prompt.', 'error')
  }
}

const importBulkAiData = () => {
  clearMsg()
  const selected = bulkUploadItems.filter((x) => bulkSelectedKeys.value.has(x.key))
  if (selected.length === 0) {
    setMsg('Vui lòng chọn video cần import.', 'error')
    return
  }

  const raw = (bulkAiImportText.value ?? '').toString().trim()
  if (!raw) return

  let list: any[] = []
  try {
    const json = JSON.parse(raw)
    if (Array.isArray(json)) {
      list = json
    } else if (json && Array.isArray((json as any).items)) {
      list = (json as any).items
    } else {
      setMsg('JSON không đúng format. Cần array hoặc object có items[].', 'error')
      return
    }
  } catch (e: any) {
    setMsg(e?.message ?? 'JSON parse error.', 'error')
    return
  }

  if (list.length === 0) {
    setMsg('Không có item nào để import.', 'error')
    return
  }

  const maxTitleLen = bulkBaseTitleMaxLen.value
  const maxDescLen = Math.max(0, 5000 - (bulkSuffixDescription.value ?? '').length)

  let applied = 0
  for (let i = 0; i < selected.length; i++) {
    const it = selected[i]
    if (!it) continue
    const x = list[i]
    if (!x) break

    const title = (x?.title ?? x?.Title ?? '').toString()
    const desc = (x?.description ?? x?.Description ?? '').toString()
    if (title.trim()) it.title = clampYoutubeTitleWithMax(title, maxTitleLen)
    if (desc.trim()) it.description = desc.length > maxDescLen ? desc.slice(0, maxDescLen) : desc
    applied += 1
  }

  setMsg(`Đã import dữ liệu cho ${applied} video.`, 'success')
}

const bulkSuffixTags = ref('')
const bulkSuffixTitle = ref('')
const bulkSuffixDescription = ref('')

const bulkBaseTitleMaxLen = computed(() => {
  const x = (bulkSuffixTitle.value ?? '').length
  return Math.max(0, 100 - x)
})

const bulkPremiereSchedule = reactive({
  startAtLocal: '',
  intervalMinutes: 10,
})

const uploadSelectedPlaylistIds = ref<Set<string>>(new Set())
const uploadSelectedChannelIds = ref<string[]>([])

const handleUploadClick = async () => {
  if ((channels.value ?? []).length === 0) return
  await openUpload()
}

const handleBulkUploadClick = async () => {
  if ((channels.value ?? []).length === 0) return
  await openBulkUpload()
}

const openUpload = async () => {
  bulkUploadOpen.value = false
  uploadForm.title = ''
  uploadForm.description = ''
  uploadForm.uploadStatus = 'public'
  uploadForm.tagsText = ''
  uploadForm.publishAtLocal = ''
  uploadSelectedPlaylistIds.value = new Set()
  uploadFile.value = null
  uploadProgress.value = 0
  uploadOpen.value = true

  if (uploadSelectedChannelIds.value.length === 0 && videoSelectedChannelIds.value.length > 0) {
    uploadSelectedChannelIds.value = Array.from(new Set(videoSelectedChannelIds.value))
  }

  if (uploadSelectedChannelIds.value.length === 1) {
    const channelId = uploadSelectedChannelIds.value[0]
    if (channelId) {
      await loadPlaylistsForUpload(channelId)
      await loadSelectedChannelDefaults(channelId)
    }
  }
}

const openBulkUpload = async () => {
  uploadOpen.value = false
  bulkUploadItems.splice(0, bulkUploadItems.length)
  bulkUploading.value = false
  bulkProgress.value = 0
  bulkUploadOpen.value = true

  bulkSuffixLastApplied.channelId = ''
  bulkSuffixLastApplied.tags = ''
  bulkSuffixLastApplied.title = ''
  bulkSuffixLastApplied.description = ''

  if (!bulkSuffixTags.value.trim() && ytChannelDefaults.defaultTags) {
    bulkSuffixTags.value = ytChannelDefaults.defaultTags
  }

  if (!bulkPremiereSchedule.startAtLocal.trim()) {
    const now = new Date(Date.now() + 7 * 60 * 60 * 1000)
    const yyyy = now.getUTCFullYear()
    const mm = String(now.getUTCMonth() + 1).padStart(2, '0')
    const dd = String(now.getUTCDate()).padStart(2, '0')
    const hh = String(now.getUTCHours()).padStart(2, '0')
    const mi = String(now.getUTCMinutes()).padStart(2, '0')
    bulkPremiereSchedule.startAtLocal = `${yyyy}-${mm}-${dd}T${hh}:${mi}`
  }

  if (uploadSelectedChannelIds.value.length === 0 && videoSelectedChannelIds.value.length > 0) {
    uploadSelectedChannelIds.value = Array.from(new Set(videoSelectedChannelIds.value))
  }

  if (uploadSelectedChannelIds.value.length === 1) {
    const channelId = uploadSelectedChannelIds.value[0]
    if (channelId) {
      await loadPlaylistsForUpload(channelId)
      await loadSelectedChannelDefaults(channelId)
    }
  }
}

const closeUpload = () => {
  uploadOpen.value = false
}

const closeBulkUpload = () => {
  bulkUploadOpen.value = false
}

const ytChannelDefaults = reactive<{ defaultDescription: string; defaultTags: string }>({
  defaultDescription: '',
  defaultTags: '',
})

const bulkSuffixLastApplied = reactive<{ channelId: string; tags: string; title: string; description: string }>({
  channelId: '',
  tags: '',
  title: '',
  description: '',
})
const savingDefaults = ref(false)

const analyticsLoading = ref(false)
const analytics = reactive<YoutubeAnalyticsSummary>({})

const channelOptions = computed(() => (channels.value ?? []).map((c) => ({ value: c.id, label: c.title })))
const singleVideoChannelId = computed(() => (videoSelectedChannelIds.value.length === 1 ? videoSelectedChannelIds.value[0] : ''))
const singleUploadChannelId = computed(() => (uploadSelectedChannelIds.value.length === 1 ? uploadSelectedChannelIds.value[0] : ''))

const setMsg = (msg: string, type: 'success' | 'error' = 'success') => {
  message.value = msg
  messageType.value = type
}

const clearMsg = () => {
  message.value = ''
}

const formatCount = (v?: number | null) => {
  if (v === null || v === undefined) return '-'
  try {
    return new Intl.NumberFormat('vi-VN').format(v)
  } catch {
    return String(v)
  }
}

const formatDateTime = (iso?: string | null) => {
  if (!iso) return '-'
  const d = new Date(iso)
  if (Number.isNaN(d.getTime())) return iso
  return new Intl.DateTimeFormat('vi-VN', {
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
  }).format(d)
}

const buildYoutubeAffiliateDescription = (description: string | null | undefined, affiliateLink: string) => {
  const desc = (description ?? '').toString()
  const link = (affiliateLink ?? '').toString().trim()
  if (!link) return desc

  const nextLine = `Xem chi tiết: ${link}`
  const shopeeRegex = /(?:Ở\s+đây\s+nè:\s*)?https?:\/\/(?:s\.)?shopee(?:\.vn|\.co|\.sg|\.ph|\.th|\.com\.my|\.tw|\.com\.br|\.com\.mx|\.cl|\.pl)\S*/gi

  if (shopeeRegex.test(desc)) {
    return desc.replace(shopeeRegex, nextLine)
  }

  return desc.trim() ? `${nextLine}\n${desc}` : nextLine
}

const extractShopeeShortLinks = (text: string | null | undefined) => {
  const input = (text ?? '').toString()
  if (!input.trim()) return [] as string[]

  const matches = input.match(/https?:\/\/s\.shopee\.vn\/[^\s<>"\)\]]+/gi) ?? []
  const unique = new Set<string>()
  for (const raw of matches) {
    const normalized = (raw ?? '').toString().trim().replace(/[.,;:!?]+$/g, '')
    if (!normalized) continue
    unique.add(normalized)
  }
  return Array.from(unique)
}

const loadAffiliatePicker = async () => {
  affiliatePickerLoading.value = true
  affiliatePickerError.value = ''
  try {
    const q = affiliatePickerAppliedSearch.value.trim()
    const qs = new URLSearchParams()
    if (q) qs.set('search', q)
    qs.set('page', String(affiliatePickerPage.value))
    qs.set('pageSize', String(affiliatePickerPageSize.value))
    const data = await api.get<AffiliatePickerResponse>(`admin/affiliate-picker/missing-social-links?${qs.toString()}`)
    affiliatePickerItems.value = Array.isArray(data?.items) ? data.items : []
    affiliatePickerTotal.value = typeof data?.total === 'number' ? data.total : 0
    affiliatePickerPage.value = typeof data?.page === 'number' ? data.page : affiliatePickerPage.value
    affiliatePickerPageSize.value = typeof data?.pageSize === 'number' ? data.pageSize : affiliatePickerPageSize.value
  } catch (e: any) {
    affiliatePickerItems.value = []
    affiliatePickerTotal.value = 0
    affiliatePickerError.value = e?.message ?? 'Không tải được danh sách sản phẩm.'
  } finally {
    affiliatePickerLoading.value = false
  }
}

const applyAffiliatePickerSearch = async () => {
  affiliatePickerAppliedSearch.value = affiliatePickerSearch.value
  affiliatePickerPage.value = 1
  await loadAffiliatePicker()
}

const goAffiliatePickerPage = async (nextPage: number) => {
  const p = Math.max(1, Math.min(nextPage, affiliatePickerTotalPages.value))
  if (p === affiliatePickerPage.value) return
  affiliatePickerPage.value = p
  await loadAffiliatePicker()
}

const selectAffiliatePick = (item: AffiliatePickerItem) => {
  affiliatePickerSelected.value = item
}

const closeAffiliatePicker = () => {
  if (affiliatePickerLoading.value || affiliatePickerApplying.value) return
  affiliatePickerOpen.value = false
}

const openAffiliatePicker = async (video: YoutubeVideoRow) => {
  clearMsg()
  if (!singleVideoChannelId.value) {
    setMsg('Vui lòng chọn đúng 1 kênh để cập nhật video.', 'error')
    return
  }

  affiliatePickerTargetVideo.value = video
  affiliatePickerSelected.value = null
  affiliatePickerSearch.value = ''
  affiliatePickerAppliedSearch.value = ''
  affiliatePickerPage.value = 1
  affiliatePickerPageSize.value = 20
  affiliatePickerTotal.value = 0
  affiliatePickerError.value = ''
  affiliatePickerOpen.value = true
  await loadAffiliatePicker()
}

const confirmAffiliatePicker = async () => {
  clearMsg()
  const video = affiliatePickerTargetVideo.value
  const selected = affiliatePickerSelected.value
  if (!video || !selected?.affiliateLink) return
  if (!singleVideoChannelId.value) {
    setMsg('Vui lòng chọn đúng 1 kênh để cập nhật video.', 'error')
    return
  }

  affiliatePickerApplying.value = true
  workingId.value = video.key
  try {
    const tags = Array.isArray(video.tags) ? video.tags.filter(Boolean) : undefined
    const nextDescription = buildYoutubeAffiliateDescription(video.description, selected.affiliateLink)
    const socialLink = `https://www.youtube.com/watch?v=${video.id}`
    await api.put<void>(`youtube/videos/${encodeURIComponent(video.id)}`, {
      channelId: singleVideoChannelId.value,
      title: clampYoutubeTitle(video.title ?? ''),
      description: nextDescription,
      privacyStatus: video.privacyStatus,
      tags,
    })

    const shortLinks = extractShopeeShortLinks(nextDescription)
    await api.post('admin/youtube-social-links/refresh-existing', {
      socialLink,
      items: shortLinks.map((shortLink) => ({
        shortLink,
      })),
    })

    videos.value = (videos.value ?? []).map((x) => (x.key === video.key ? { ...x, description: nextDescription } : x))
    setMsg('Đã thêm link Affiliate vào mô tả video.', 'success')
    affiliatePickerOpen.value = false
  } catch (e: any) {
    affiliatePickerError.value = e?.message ?? 'Không cập nhật được video.'
    setMsg(e?.message ?? 'Không cập nhật được video.', 'error')
  } finally {
    affiliatePickerApplying.value = false
    workingId.value = null
  }
}

const reload = async () => {
  clearMsg()
  await loadChannels()
  if (activeTab.value === 'videos' && videoSelectedChannelIds.value.length > 0) {
    await loadVideos()
  }
  if (activeTab.value === 'analytics' && singleVideoChannelId.value) {
    await loadAnalytics()
  }
}

const loadEditInfo = async () => {
  if (!singleVideoChannelId.value || !editForm.id) return
  try {
    const data = await api.get<{ tags?: string[]; playlistIds?: string[] }>(
      `youtube/channels/${encodeURIComponent(singleVideoChannelId.value)}/videos/${encodeURIComponent(editForm.id)}/edit-info`,
    )

    const tags = Array.isArray(data?.tags) ? data.tags : []
    editForm.tagsText = tags.join(', ')

    const ids = Array.isArray(data?.playlistIds) ? data.playlistIds : []
    selectedPlaylistIds.value = new Set(ids)
  } catch (e: any) {
    // ignore
  }
}

const loadSelectedChannelDefaults = async (channelId: string) => {
  if (!channelId) return
  try {
    const data = await api.get<any>(`youtube/channels/${encodeURIComponent(channelId)}`)
    ytChannelDefaults.defaultDescription = data?.defaultDescription ?? ''
    ytChannelDefaults.defaultTags = data?.defaultTags ?? ''

    const defaultSuffixTitle = (data?.defaultSuffixTitle ?? data?.defaultTitle ?? '') as string

    const canApplyBulk = bulkUploadOpen.value && uploadSelectedChannelIds.value.length === 1 && uploadSelectedChannelIds.value[0] === channelId
    if (canApplyBulk) {
      const nextTags = (ytChannelDefaults.defaultTags ?? '').toString()
      const nextDesc = (ytChannelDefaults.defaultDescription ?? '').toString()
      const nextTitle = (defaultSuffixTitle ?? '').toString()

      const shouldApplyTags = !bulkSuffixTags.value.trim() || bulkSuffixTags.value === bulkSuffixLastApplied.tags
      const shouldApplyDesc = !bulkSuffixDescription.value.trim() || bulkSuffixDescription.value === bulkSuffixLastApplied.description
      const shouldApplyTitle = !bulkSuffixTitle.value.trim() || bulkSuffixTitle.value === bulkSuffixLastApplied.title

      if (shouldApplyTags) bulkSuffixTags.value = nextTags
      if (shouldApplyDesc) bulkSuffixDescription.value = nextDesc
      if (shouldApplyTitle && nextTitle.trim()) bulkSuffixTitle.value = nextTitle

      bulkSuffixLastApplied.channelId = channelId
      bulkSuffixLastApplied.tags = nextTags
      bulkSuffixLastApplied.title = nextTitle
      bulkSuffixLastApplied.description = nextDesc
    }

    if (!uploadForm.description.trim() && ytChannelDefaults.defaultDescription) {
      uploadForm.description = ytChannelDefaults.defaultDescription
    }
    if (!uploadForm.tagsText.trim() && ytChannelDefaults.defaultTags) {
      uploadForm.tagsText = ytChannelDefaults.defaultTags
    }
  } catch {
    ytChannelDefaults.defaultDescription = ''
    ytChannelDefaults.defaultTags = ''
  }
}

const saveSelectedChannelDefaults = async () => {
  clearMsg()
  const ids = Array.from(new Set(uploadSelectedChannelIds.value ?? [])).filter(Boolean)
  if (ids.length === 0) {
    setMsg('Vui lòng chọn kênh upload.', 'error')
    return
  }

  savingDefaults.value = true
  try {
    let okCount = 0
    const errors: string[] = []
    for (const cid of ids) {
      try {
        await api.put(`youtube/channels/${encodeURIComponent(cid)}/defaults`, {
          defaultDescription: uploadForm.description.trim() || null,
          defaultTags: uploadForm.tagsText.trim() || null,
        })
        okCount += 1
      } catch (e: any) {
        errors.push(`${cid}: ${e?.message ?? 'save fail'}`)
      }
    }

    ytChannelDefaults.defaultDescription = uploadForm.description.trim() || ''
    ytChannelDefaults.defaultTags = uploadForm.tagsText.trim() || ''

    if (errors.length === 0) {
      setMsg(`Đã lưu thiết lập mặc định cho ${okCount} kênh.`, 'success')
    } else {
      setMsg(`Lưu mặc định xong ${okCount}/${ids.length}. Lỗi: ${errors.join(' | ')}`, 'error')
    }
  } catch (e: any) {
    setMsg(e?.message ?? 'Không lưu được thiết lập mặc định.', 'error')
  } finally {
    savingDefaults.value = false
  }
}

const loadChannels = async () => {
  loading.value = true
  try {
    const data = await api.get<YoutubeChannel[]>('youtube/channels')
    channels.value = Array.isArray(data) ? data : []
  } catch (e: any) {
    setMsg(e?.message ?? 'Không tải được danh sách kênh.', 'error')
  } finally {
    loading.value = false
  }
}

const loadPlaylistsForUpload = async (channelId: string) => {
  if (!channelId) return
  playlistsLoading.value = true
  try {
    const data = await api.get<{ id: string; title: string }[]>(`youtube/channels/${encodeURIComponent(channelId)}/playlists`)
    playlists.value = Array.isArray(data) ? data : []
  } catch {
    playlists.value = []
  } finally {
    playlistsLoading.value = false
  }
}

watch(
  () => singleUploadChannelId.value,
  async (cid) => {
    uploadSelectedPlaylistIds.value = new Set()
    playlists.value = []
    if (!cid) return
    await loadPlaylistsForUpload(cid)
    await loadSelectedChannelDefaults(cid)
  },
)

watch(
  () => singleVideoChannelId.value,
  async (cid) => {
    bulkPlaylistId.value = ''
    if (!cid) {
      playlists.value = []
      return
    }
    await loadPlaylistsForBulk()
  },
)

const toggleUploadPlaylist = (playlistId: string, checked: boolean) => {
  if (checked) {
    uploadSelectedPlaylistIds.value.add(playlistId)
  } else {
    uploadSelectedPlaylistIds.value.delete(playlistId)
  }
  uploadSelectedPlaylistIds.value = new Set(uploadSelectedPlaylistIds.value)
}

const clampYoutubeTitle = (v: string) => {
  const s = (v ?? '').toString()
  return s.length > 100 ? s.slice(0, 100) : s
}

const clampYoutubeTitleWithMax = (v: string, maxLen: number) => {
  const s = (v ?? '').toString()
  const m = Math.max(0, Math.floor(Number(maxLen) || 0))
  return s.length > m ? s.slice(0, m) : s
}

const buildBulkTitle = (baseTitle: string) => {
  const sfx = (bulkSuffixTitle.value ?? '').toString()
  const baseMax = Math.max(0, 100 - sfx.length)
  const base = clampYoutubeTitleWithMax((baseTitle ?? '').toString(), baseMax)
  const merged = base + sfx
  return merged.length > 100 ? merged.slice(0, 100) : merged
}

const buildBulkDescription = (baseDescription: string) => {
  const sfx = (bulkSuffixDescription.value ?? '').toString()
  const merged = ((baseDescription ?? '').toString() + sfx)
  return merged.length > 5000 ? merged.slice(0, 5000) : merged
}

const onPickFile = (e: Event) => {
  const input = e.target as HTMLInputElement
  const file = input.files?.[0] ?? null
  uploadFile.value = file
  if (file && !uploadForm.title.trim()) {
    uploadForm.title = clampYoutubeTitle(file.name.replace(/\.[^.]+$/, ''))
  }
}

const onPickBulkFiles = (e: Event) => {
  const input = e.target as HTMLInputElement
  const files = Array.from(input.files ?? [])
  bulkUploadItems.splice(0, bulkUploadItems.length)

  if (!bulkPremiereSchedule.startAtLocal.trim()) {
    const now = new Date(Date.now() + 7 * 60 * 60 * 1000)
    const yyyy = now.getUTCFullYear()
    const mm = String(now.getUTCMonth() + 1).padStart(2, '0')
    const dd = String(now.getUTCDate()).padStart(2, '0')
    const hh = String(now.getUTCHours()).padStart(2, '0')
    const mi = String(now.getUTCMinutes()).padStart(2, '0')
    bulkPremiereSchedule.startAtLocal = `${yyyy}-${mm}-${dd}T${hh}:${mi}`
  }

  for (const [i, f] of files.entries()) {
    const base = f.name.replace(/\.[^.]+$/, '')
    bulkUploadItems.push({
      key: `${Date.now()}_${Math.random().toString(16).slice(2)}`,
      file: f,
      title: clampYoutubeTitleWithMax(base, bulkBaseTitleMaxLen.value),
      description: '',
      uploadStatus: i === 0 ? 'public' : 'premiere',
      tagsText: '',
      publishAtLocal: '',
      status: 'pending',
      statusLabel: 'Chờ',
    })
  }

  applyBulkPremiereSchedule()
}

watch(
  () => uploadForm.uploadStatus,
  (v) => {
    if (v !== 'premiere') {
      uploadForm.publishAtLocal = ''
    }
  },
)

watch(
  () => uploadForm.title,
  (v) => {
    const next = clampYoutubeTitle(v)
    if (next !== v) uploadForm.title = next
  },
)

watch(
  () => bulkUploadItems.map((x) => x.uploadStatus),
  () => {
    for (const it of bulkUploadItems) {
      if (it.uploadStatus !== 'premiere' && it.publishAtLocal) {
        it.publishAtLocal = ''
      }
    }
  },
)

watch(
  () => bulkUploadItems.map((x) => x.title),
  () => {
    for (const it of bulkUploadItems) {
      const next = clampYoutubeTitleWithMax(it.title, bulkBaseTitleMaxLen.value)
      if (next !== it.title) it.title = next
    }
  },
)

watch(
  () => bulkSuffixTitle.value,
  () => {
    for (const it of bulkUploadItems) {
      const next = clampYoutubeTitleWithMax(it.title, bulkBaseTitleMaxLen.value)
      if (next !== it.title) it.title = next
    }
  },
)

watch(
  () => editForm.title,
  (v) => {
    const next = clampYoutubeTitle(v)
    if (next !== v) editForm.title = next
  },
)

const clearBulkUpload = () => {
  bulkUploadItems.splice(0, bulkUploadItems.length)
  bulkProgress.value = 0
  bulkSelectedKeys.value = new Set()
  bulkAiPromptOpen.value = false
}

const toPublishAtIsoUtcFromUtc7Local = (local: string) => {
  const v = (local ?? '').trim()
  if (!v) return ''
  const d = new Date(`${v}:00+07:00`)
  if (Number.isNaN(d.getTime())) return ''
  return d.toISOString()
}

const toUtc7LocalFromUtcMs = (utcMs: number) => {
  const d = new Date(utcMs + 7 * 60 * 60 * 1000)
  const yyyy = d.getUTCFullYear()
  const mm = String(d.getUTCMonth() + 1).padStart(2, '0')
  const dd = String(d.getUTCDate()).padStart(2, '0')
  const hh = String(d.getUTCHours()).padStart(2, '0')
  const mi = String(d.getUTCMinutes()).padStart(2, '0')
  return `${yyyy}-${mm}-${dd}T${hh}:${mi}`
}

const applyBulkPremiereSchedule = () => {
  const start = (bulkPremiereSchedule.startAtLocal ?? '').trim()
  if (!start) return

  const startDate = new Date(`${start}:00+07:00`)
  if (Number.isNaN(startDate.getTime())) return

  const interval = Math.max(0, Number(bulkPremiereSchedule.intervalMinutes ?? 0))
  for (const [i, it] of bulkUploadItems.entries()) {
    if (it.uploadStatus !== 'premiere') continue

    const utcMs = startDate.getTime() + i * interval * 60 * 1000
    it.publishAtLocal = toUtc7LocalFromUtcMs(utcMs)
  }
}

watch(
  () => [bulkPremiereSchedule.startAtLocal, bulkPremiereSchedule.intervalMinutes] as const,
  () => {
    applyBulkPremiereSchedule()
  },
)

const loadVideos = async () => {
  clearMsg()
  const ids = Array.from(new Set(videoSelectedChannelIds.value ?? [])).filter(Boolean)
  if (ids.length === 0) return

  videoLoading.value = true
  try {
    const q = videoQuery.value.trim()
    const result: YoutubeVideoRow[] = []
    for (const cid of ids) {
      const url = q
        ? `youtube/channels/${encodeURIComponent(cid)}/videos?query=${encodeURIComponent(q)}`
        : `youtube/channels/${encodeURIComponent(cid)}/videos`
      const data = await api.get<YoutubeVideo[]>(url)
      const list = Array.isArray(data) ? data : []
      for (const v of list) {
        result.push({
          ...(v as YoutubeVideo),
          channelId: cid,
          key: `${cid}:${v.id}`,
        })
      }
    }
    videos.value = result
    selectedVideoIds.value = new Set()
  } catch (e: any) {
    setMsg(e?.message ?? 'Không tải được danh sách video.', 'error')
    videos.value = []
    selectedVideoIds.value = new Set()
  } finally {
    videoLoading.value = false
  }
}

const uploadVideo = async () => {
  clearMsg()
  const ids = Array.from(new Set(uploadSelectedChannelIds.value ?? [])).filter(Boolean)
  if (ids.length === 0) {
    setMsg('Vui lòng chọn kênh upload.', 'error')
    return
  }
  if (!uploadFile.value) {
    setMsg('Vui lòng chọn file video.', 'error')
    return
  }

  const ytTitleLen = (uploadForm.title ?? '').trim().length
  const ytDescLen = (uploadForm.description ?? '').trim().length
  if (ytTitleLen > 100) {
    setMsg(`Tiêu đề tối đa 100 ký tự (hiện tại ${ytTitleLen}).`, 'error')
    return
  }
  if (ytDescLen > 5000) {
    setMsg(`Mô tả tối đa 5000 ký tự (hiện tại ${ytDescLen}).`, 'error')
    return
  }

  uploading.value = true
  uploadProgress.value = 0

  try {
    if (uploadForm.uploadStatus === 'premiere' && !uploadForm.publishAtLocal.trim()) {
      setMsg('Vui lòng chọn lịch xuất bản cho trạng thái Công chiếu.', 'error')
      return
    }

    const tags = uploadForm.tagsText
      .split(',')
      .map((x) => x.trim())
      .filter(Boolean)

    const publishAtIso = toPublishAtIsoUtcFromUtc7Local(uploadForm.publishAtLocal)

    let okCount = 0
    const errors: string[] = []
    for (const cid of ids) {
      try {
        const formData = new FormData()
        formData.append('file', uploadFile.value)
        formData.append('title', uploadForm.title)
        formData.append('description', uploadForm.description)

        const privacyStatus = uploadForm.uploadStatus === 'public' ? 'public' : 'private'
        formData.append('privacyStatus', privacyStatus)
        tags.forEach((t) => formData.append('tags', t))

        if (publishAtIso) {
          formData.append('publishAt', publishAtIso)
        }

        if (ids.length === 1) {
          Array.from(uploadSelectedPlaylistIds.value).forEach((pid) => formData.append('playlistIds', pid))
        }

        await api.post<void>(`youtube/channels/${encodeURIComponent(cid)}/videos/upload`, formData, {
          headers: { 'Content-Type': 'multipart/form-data' },
          timeout: 180000,
        } as any)
        okCount += 1
      } catch (e: any) {
        errors.push(`${cid}: ${e?.message ?? 'upload fail'}`)
      }
    }

    if (errors.length === 0) {
      setMsg(`Upload thành công ${okCount} kênh.`, 'success')
      closeUpload()
      if (activeTab.value === 'videos') await loadVideos()
    } else {
      setMsg(`Upload xong ${okCount}/${ids.length}. Lỗi: ${errors.join(' | ')}`, 'error')
    }
  } catch (e: any) {
    setMsg(e?.message ?? 'Upload thất bại.', 'error')
  } finally {
    uploading.value = false
  }
}

const uploadBulkVideos = async () => {
  clearMsg()
  const ids = Array.from(new Set(uploadSelectedChannelIds.value ?? [])).filter(Boolean)
  if (ids.length === 0) {
    setMsg('Vui lòng chọn kênh upload.', 'error')
    return
  }
  if (bulkUploadItems.length === 0) return

  const itemsToUpload = bulkUploadItems.filter((x) => x.status !== 'success')
  if (itemsToUpload.length === 0) {
    setMsg('Tất cả video đã upload xong, không cần upload lại.', 'success')
    return
  }

  bulkUploading.value = true
  bulkProgress.value = 0

  try {
    const total = itemsToUpload.length * ids.length
    let done = 0
    const errors: string[] = []

    const commonTags = (bulkSuffixTags.value ?? '')
      .split(',')
      .map((x) => x.trim())
      .filter(Boolean)

    for (const it of itemsToUpload) {
      it.status = 'uploading'
      it.statusLabel = 'Đang upload'

      if (it.uploadStatus === 'premiere' && !(it.publishAtLocal ?? '').trim()) {
        it.status = 'error'
        it.statusLabel = 'Thiếu lịch công chiếu'
        errors.push(`${it.file.name}: missing publishAt`)
        done += ids.length
        bulkProgress.value = Math.round((done * 100) / Math.max(1, total))
        continue
      }

      const ytTitleLen = (it.title ?? '').trim().length
      const ytDescLen = (it.description ?? '').trim().length
      if (ytTitleLen > bulkBaseTitleMaxLen.value) {
        it.status = 'error'
        it.statusLabel = `Lỗi title (${ytTitleLen}/${bulkBaseTitleMaxLen.value})`
        errors.push(`${it.file.name}: title too long`)
        done += ids.length
        bulkProgress.value = Math.round((done * 100) / Math.max(1, total))
        continue
      }
      if (ytDescLen > 5000) {
        it.status = 'error'
        it.statusLabel = `Lỗi desc (${ytDescLen}/5000)`
        errors.push(`${it.file.name}: desc > 5000`)
        done += ids.length
        bulkProgress.value = Math.round((done * 100) / Math.max(1, total))
        continue
      }

      const publishAtIso = toPublishAtIsoUtcFromUtc7Local(it.publishAtLocal)

      let itemOk = 0
      for (const cid of ids) {
        try {
          const formData = new FormData()
          formData.append('file', it.file)
          formData.append('title', buildBulkTitle(it.title))
          formData.append('description', buildBulkDescription(it.description))

          const privacyStatus = it.uploadStatus === 'public' ? 'public' : 'private'
          formData.append('privacyStatus', privacyStatus)
          commonTags.forEach((t) => formData.append('tags', t))

          if (publishAtIso) {
            formData.append('publishAt', publishAtIso)
          }

          if (ids.length === 1) {
            Array.from(uploadSelectedPlaylistIds.value).forEach((pid) => formData.append('playlistIds', pid))
          }

          await api.post<void>(`youtube/channels/${encodeURIComponent(cid)}/videos/upload`, formData, {
            headers: { 'Content-Type': 'multipart/form-data' },
            timeout: 180000,
          } as any)
          itemOk += 1
        } catch (e: any) {
          errors.push(`${it.file.name} -> ${cid}: ${e?.message ?? 'upload fail'}`)
        } finally {
          done += 1
          bulkProgress.value = Math.round((done * 100) / Math.max(1, total))
        }
      }

      if (itemOk === ids.length) {
        it.status = 'success'
        it.statusLabel = 'Xong'
      } else if (itemOk > 0) {
        it.status = 'error'
        it.statusLabel = `Lỗi (${itemOk}/${ids.length})`
      } else {
        it.status = 'error'
        it.statusLabel = 'Lỗi'
      }
    }

    if (errors.length === 0) {
      setMsg(`Upload hàng loạt thành công ${itemsToUpload.length} video.`, 'success')
      closeBulkUpload()
      if (activeTab.value === 'videos') await loadVideos()
    } else {
      setMsg(`Upload hàng loạt xong. Lỗi: ${errors.slice(0, 5).join(' | ')}${errors.length > 5 ? ' ...' : ''}`, 'error')
    }
  } catch (e: any) {
    setMsg(e?.message ?? 'Upload hàng loạt thất bại.', 'error')
  } finally {
    bulkUploading.value = false
  }
}

const loadAnalytics = async () => {
  clearMsg()
  if (!singleVideoChannelId.value) {
    setMsg('Vui lòng chọn đúng 1 kênh ở tab Video để xem analytics.', 'error')
    return
  }

  analyticsLoading.value = true
  try {
    const data = await api.get<YoutubeAnalyticsSummary>(`youtube/channels/${encodeURIComponent(singleVideoChannelId.value)}/analytics/summary`)
    analytics.watchTimeMinutes = data?.watchTimeMinutes ?? null
    analytics.views = data?.views ?? null
    analytics.avgViewDurationSec = data?.avgViewDurationSec ?? null
    analytics.subsGained = data?.subsGained ?? null
  } catch (e: any) {
    setMsg(e?.message ?? 'Không tải được analytics.', 'error')
    analytics.watchTimeMinutes = null
    analytics.views = null
    analytics.avgViewDurationSec = null
    analytics.subsGained = null
  } finally {
    analyticsLoading.value = false
  }
}

const connectChannel = async () => {
  clearMsg()
  connecting.value = true
  try {
    const data = await api.get<{ url: string }>('youtube/oauth/url')
    const url = data?.url
    if (!url) {
      setMsg('Không lấy được URL kết nối YouTube.', 'error')
      return
    }
    window.location.href = url
  } catch (e: any) {
    setMsg(e?.message ?? 'Không kết nối được YouTube.', 'error')
  } finally {
    connecting.value = false
  }
}

const reauthorize = async (channelId: string) => {
  clearMsg()
  if (!channelId) return
  workingId.value = channelId
  try {
    const data = await api.get<{ url: string }>(`youtube/channels/${encodeURIComponent(channelId)}/oauth/url`)
    const url = data?.url
    if (!url) {
      setMsg('Không lấy được URL auth lại.', 'error')
      return
    }
    window.location.href = url
  } catch (e: any) {
    setMsg(e?.message ?? 'Không auth lại được.', 'error')
  } finally {
    workingId.value = null
  }
}

const disconnect = async (channelId: string) => {
  clearMsg()
  if (!channelId) return
  workingId.value = channelId
  try {
    await api.delete<void>(`youtube/channels/${encodeURIComponent(channelId)}`)
    setMsg('Đã xoá kênh.', 'success')
    videoSelectedChannelIds.value = (videoSelectedChannelIds.value ?? []).filter((x) => x !== channelId)
    uploadSelectedChannelIds.value = (uploadSelectedChannelIds.value ?? []).filter((x) => x !== channelId)
    await loadChannels()
  } catch (e: any) {
    setMsg(e?.message ?? 'Không xoá được kênh.', 'error')
  } finally {
    workingId.value = null
  }
}

const toggleVideoSelection = (key: string, checked: boolean) => {
  if (checked) {
    selectedVideoIds.value.add(key)
  } else {
    selectedVideoIds.value.delete(key)
  }
  selectedVideoIds.value = new Set(selectedVideoIds.value)
}

const clearSelection = () => {
  selectedVideoIds.value = new Set()
}

const parseVideoKey = (key: string) => {
  const idx = key.indexOf(':')
  if (idx < 0) return { channelId: '', videoId: key }
  return { channelId: key.slice(0, idx), videoId: key.slice(idx + 1) }
}

const closeEdit = () => {
  editOpen.value = false
}

const closeBulkEdit = () => {
  bulkEditOpen.value = false
  bulkEditSelectedChannelIds.value = []
}

const openBulkEdit = () => {
  bulkEditForm.privacyStatus = ''

  bulkEditSuffixTags.value = ''
  bulkEditSuffixTitle.value = ''
  bulkEditSuffixDescription.value = ''
  bulkEditApplySuffixTags.value = false
  bulkEditApplySuffixTitle.value = false
  bulkEditApplySuffixDescription.value = false

  bulkEditSuffixLastApplied.channelId = ''
  bulkEditSuffixLastApplied.tags = ''
  bulkEditSuffixLastApplied.title = ''
  bulkEditSuffixLastApplied.description = ''

  bulkEditAiImportOpen.value = false
  bulkEditAiImportText.value = ''

  const selected = (videos.value ?? []).filter((x) => selectedVideoIds.value.has(x.key))
  const channelIds = Array.from(new Set(selected.map((x) => x.channelId).filter(Boolean)))
  bulkEditSelectedChannelIds.value = channelIds
  bulkEditOpen.value = true

  // Initialize editable fields from current values
  for (const v of selected) {
    v._editTitle = v.title ?? ''
    v._editDescription = v.description ?? ''
    v._editPrivacy = v.privacyStatus ?? 'public'
    v._editTagsText = Array.isArray(v.tags) ? v.tags.join(', ') : ''

    v._origTitle = v.title ?? ''
    v._origDescription = v.description ?? ''

    v._editPublishAtLocal = ''
  }
}

const applyBulkEditPremiereSchedule = (auto = false) => {
  const start = (bulkEditPremiereSchedule.startAtLocal ?? '').trim()
  if (!start) {
    if (!auto) setMsg('Vui lòng chọn thời gian bắt đầu (UTC+7) trước khi áp dụng.', 'error')
    return
  }

  const startDate = new Date(`${start}:00+07:00`)
  if (Number.isNaN(startDate.getTime())) return

  const interval = Math.max(0, Number(bulkEditPremiereSchedule.intervalMinutes ?? 0))
  const selectedRows = (videos.value ?? []).filter((x) => selectedVideoIds.value.has(x.key))
  if (!auto && selectedRows.length === 0) {
    setMsg('Chưa chọn video nào để áp dụng lịch công chiếu.', 'error')
    return
  }

  if (auto) {
    let k = 0
    for (const v of selectedRows) {
      const p = (v._editPrivacy ?? v.privacyStatus ?? 'public')
      if (p === 'public' || p === 'premiere') continue

      const utcMs = startDate.getTime() + k * interval * 60 * 1000
      v._editPublishAtLocal = toUtc7LocalFromUtcMs(utcMs)
      k += 1
    }
  }

  // Manual apply:
  // - skip videos already public
  // - first non-public -> public
  // - subsequent non-public -> premiere + schedule
  const eligibles = selectedRows.filter((v) => {
    const p = (v._editPrivacy ?? v.privacyStatus ?? 'public')
    return p !== 'public' && p !== 'premiere'
  })
  if (eligibles.length === 0) {
    setMsg('Không có video nào phù hợp để áp dụng (chỉ áp dụng cho video != public và != premiere).', 'success')
    return
  }

  const skippedPublic = selectedRows.filter((v) => (v._editPrivacy ?? v.privacyStatus ?? 'public') === 'public').length
  const skippedPremiere = selectedRows.filter((v) => (v._editPrivacy ?? v.privacyStatus ?? 'public') === 'premiere').length

  const updates = new Map<string, { _editPrivacy: string; _editPublishAtLocal: string }>()
  const firstEligible = eligibles[0]
  if (!firstEligible) return
  updates.set(firstEligible.key, { _editPrivacy: 'public', _editPublishAtLocal: '' })

  for (let i = 1; i < eligibles.length; i++) {
    const v = eligibles[i]
    if (!v) continue
    const utcMs = startDate.getTime() + i * interval * 60 * 1000
    updates.set(v.key, { _editPrivacy: 'premiere', _editPublishAtLocal: toUtc7LocalFromUtcMs(utcMs) })
  }

  videos.value = (videos.value ?? []).map((row) => {
    const u = updates.get(row.key)
    if (!u) return row
    return {
      ...row,
      _editPrivacy: u._editPrivacy,
      _editPublishAtLocal: u._editPublishAtLocal,
    }
  })

  const sample = eligibles
    .slice(0, 3)
    .map((x) => `${x.id}:${updates.get(x.key)?._editPrivacy ?? ''}`)
    .join(', ')

  setMsg(
    `Đã áp dụng: bỏ qua ${skippedPublic} video public, ${skippedPremiere} video premiere; 1 video chuyển public; ${Math.max(0, eligibles.length - 1)} video chuyển premiere & set lịch.${sample ? ' Ví dụ: ' + sample : ''}`,
    'success',
  )
}

// Removed automatic watcher for bulkEditPremiereSchedule
// User now needs to manually click "Áp dụng" button to apply premiere schedule settings

const loadBulkEditChannelDefaults = async (channelId: string) => {
  if (!channelId) return
  if (!bulkEditOpen.value) return
  if (!bulkEditSelectedChannelIds.value || bulkEditSelectedChannelIds.value.length !== 1) return
  if (bulkEditSelectedChannelIds.value[0] !== channelId) return

  try {
    const data = await api.get<any>(`youtube/channels/${encodeURIComponent(channelId)}`)
    const nextTags = (data?.defaultTags ?? '').toString()
    const nextDesc = (data?.defaultDescription ?? '').toString()
    const nextTitle = ((data?.defaultSuffixTitle ?? data?.defaultTitle ?? '') as string).toString()

    const shouldApplyTags = !bulkEditSuffixTags.value.trim() || bulkEditSuffixTags.value === bulkEditSuffixLastApplied.tags
    const shouldApplyDesc = !bulkEditSuffixDescription.value.trim() || bulkEditSuffixDescription.value === bulkEditSuffixLastApplied.description
    const shouldApplyTitle = !bulkEditSuffixTitle.value.trim() || bulkEditSuffixTitle.value === bulkEditSuffixLastApplied.title

    if (shouldApplyTags) bulkEditSuffixTags.value = nextTags
    if (shouldApplyDesc) bulkEditSuffixDescription.value = nextDesc
    if (shouldApplyTitle && nextTitle.trim()) bulkEditSuffixTitle.value = nextTitle

    bulkEditSuffixLastApplied.channelId = channelId
    bulkEditSuffixLastApplied.tags = nextTags
    bulkEditSuffixLastApplied.title = nextTitle
    bulkEditSuffixLastApplied.description = nextDesc
  } catch {
    // ignore
  }
}

watch(
  () => bulkEditSelectedChannelIds.value,
  async (ids) => {
    if (!bulkEditOpen.value) return
    const cid = Array.isArray(ids) && ids.length === 1 ? ids[0] : ''
    if (!cid) return
    await loadBulkEditChannelDefaults(cid)
  },
)

const openEdit = async (v: YoutubeVideoRow) => {
  clearMsg()
  if (!singleVideoChannelId.value) {
    setMsg('Vui lòng chọn đúng 1 kênh để sửa video.', 'error')
    return
  }

  editForm.id = v.id
  editForm.title = clampYoutubeTitle(v.title ?? '')
  editForm.description = v.description ?? ''
  editForm.privacyStatus = (v.privacyStatus as any) ?? 'public'
  editForm.tagsText = ''
  editForm.thumbFile = null
  editForm.thumbPreview = v.thumbnailUrl ?? ''
  selectedPlaylistIds.value = new Set()
  editOpen.value = true

  await loadPlaylistsForEdit()
  await loadEditInfo()
}

const saveEdit = async () => {
  clearMsg()
  if (!editForm.id) return
  if (!editForm.title.trim()) {
    setMsg('Title là bắt buộc.', 'error')
    return
  }

  const tags = editForm.tagsText
    .split(',')
    .map((x) => x.trim())
    .filter(Boolean)

  savingEdit.value = true
  workingId.value = `${singleVideoChannelId.value}:${editForm.id}`
  try {
    await api.put<void>(`youtube/videos/${encodeURIComponent(editForm.id)}`, {
      channelId: singleVideoChannelId.value,
      title: editForm.title,
      description: editForm.description,
      privacyStatus: editForm.privacyStatus,
      tags,
    })
    setMsg('Đã lưu video.', 'success')
    closeEdit()
    await loadVideos()
  } catch (e: any) {
    setMsg(e?.message ?? 'Không lưu được video.', 'error')
  } finally {
    savingEdit.value = false
    workingId.value = null
  }
}

const deleteVideo = async (videoId: string) => {
  clearMsg()
  if (!videoId) return
  workingId.value = `${singleVideoChannelId.value}:${videoId}`
  try {
    await api.delete<void>(`youtube/videos/${encodeURIComponent(videoId)}`)
    setMsg('Đã xoá video.', 'success')
    await loadVideos()
  } catch (e: any) {
    setMsg(e?.message ?? 'Không xoá được video.', 'error')
  } finally {
    workingId.value = null
  }
}

const bulkAddToPlaylist = async () => {
  clearMsg()
  if (!singleVideoChannelId.value) {
    setMsg('Vui lòng chọn đúng 1 kênh để thao tác playlist.', 'error')
    return
  }
  if (!bulkPlaylistId.value) return
  const videoIds = Array.from(selectedVideoIds.value)
    .map((k) => parseVideoKey(k).videoId)
    .filter(Boolean)
  if (videoIds.length === 0) return

  bulkWorking.value = true
  try {
    await api.post<void>(`youtube/playlists/${encodeURIComponent(bulkPlaylistId.value)}/videos:bulk`, {
      videoIds,
    })
    setMsg('Đã thêm vào playlist.', 'success')
  } catch (e: any) {
    setMsg(e?.message ?? 'Không thêm được vào playlist.', 'error')
  } finally {
    bulkWorking.value = false
  }
}

const saveBulkEdit = async () => {
  clearMsg()
  if (!bulkEditSelectedChannelIds.value || bulkEditSelectedChannelIds.value.length !== 1) {
    setMsg('Vui lòng chọn đúng 1 kênh để sửa hàng loạt.', 'error')
    return
  }

  const suffixTitle = bulkEditApplySuffixTitle.value ? (bulkEditSuffixTitle.value ?? '').toString() : ''
  const suffixDesc = bulkEditApplySuffixDescription.value ? (bulkEditSuffixDescription.value ?? '').toString() : ''
  const suffixTagsInput = bulkEditApplySuffixTags.value ? (bulkEditSuffixTags.value ?? '').toString().trim() : ''

  const videoIds = videos.value
    .filter((x) => selectedVideoIds.value.has(x.key))
    .map((x) => x.id)
    .filter(Boolean)
  if (videoIds.length === 0) return

  bulkWorking.value = true
  try {
    const suffixTags = suffixTagsInput
      ? suffixTagsInput
          .split(',')
          .map((x) => x.trim())
          .filter(Boolean)
      : []

    const selectedRows = (videos.value ?? []).filter((x) => selectedVideoIds.value.has(x.key))
    let ok = 0
    const errs: string[] = []

    for (const v of selectedRows) {
      try {
        const baseTitle = (v._editTitle ?? '').toString()
        const baseDesc = (v._editDescription ?? '').toString()
        const nextTitle = (baseTitle + suffixTitle).toString()
        const nextDesc = (baseDesc + suffixDesc).toString()

        const origTitle = (v._origTitle ?? (v.title ?? '')).toString()
        const origDesc = (v._origDescription ?? (v.description ?? '')).toString()

        // tags
        let nextTags: string[] | null = null
        if (v._editTagsText != null && v._editTagsText.trim() !== '') {
          nextTags = v._editTagsText
            .split(',')
            .map((x) => x.trim())
            .filter(Boolean)
        } else if (suffixTags.length > 0) {
          // fetch current tags only when we need to append
          const channelId = bulkEditSelectedChannelIds.value[0]
          if (!channelId) throw new Error('Vui lòng chọn đúng 1 kênh để cập nhật tags.')
          const info = await api.get<{ tags?: string[] }>(
            `youtube/channels/${encodeURIComponent(channelId)}/videos/${encodeURIComponent(v.id)}/edit-info`,
          )
          const current = Array.isArray(info?.tags) ? info.tags : []
          nextTags = [...current, ...suffixTags]
        }

        if (nextTags) {
          nextTags = nextTags.map((x) => (x ?? '').toString().trim()).filter(Boolean)
          // de-dup (case-insensitive)
          const seen = new Set<string>()
          nextTags = nextTags.filter((t) => {
            const k = t.toLowerCase()
            if (seen.has(k)) return false
            seen.add(k)
            return true
          })
        }

        const payload: any = {}

        payload.channelId = bulkEditSelectedChannelIds.value[0]

        // Backend requires Title, so always send it.
        payload.title = clampYoutubeTitle(nextTitle)

        // Only update description when user changed it or suffixDesc is provided.
        // This prevents wiping existing descriptions when the UI didn't load them.
        const editedDescChanged = baseDesc !== origDesc
        if (suffixDesc.trim() || editedDescChanged) {
          payload.description = nextDesc.length > 5000 ? nextDesc.slice(0, 5000) : nextDesc
        }

        const privacyRaw = (v._editPrivacy ?? '').trim() || (v.privacyStatus ?? 'public')
        const isPremiere = privacyRaw === 'premiere'
        payload.privacyStatus = isPremiere ? 'private' : privacyRaw

        if (isPremiere) {
          const local = (v._editPublishAtLocal ?? '').toString().trim()
          if (!local) {
            throw new Error('Thiếu lịch công chiếu')
          }
          const publishAtIso = toPublishAtIsoUtcFromUtc7Local(local)
          if (publishAtIso) payload.publishAt = publishAtIso
        }
        if (nextTags !== null) payload.tags = nextTags

        await api.put<void>(`youtube/videos/${encodeURIComponent(v.id)}`, payload)
        ok += 1
      } catch (e: any) {
        errs.push(`${v.id}: ${e?.message ?? 'update fail'}`)
      }
    }

    if (errs.length === 0) {
      setMsg(`Đã cập nhật hàng loạt ${ok}/${selectedRows.length}.`, 'success')
    } else {
      setMsg(`Cập nhật xong ${ok}/${selectedRows.length}. Lỗi: ${errs.slice(0, 5).join(' | ')}`, 'error')
    }

    closeBulkEdit()
    await loadVideos()
  } catch (e: any) {
    setMsg(e?.message ?? 'Không cập nhật được.', 'error')
  } finally {
    bulkWorking.value = false
  }
}

const importBulkEditAiData = () => {
  const txt = (bulkEditAiImportText.value ?? '').toString().trim()
  if (!txt) return
  let parsed: any[] = []
  try {
    parsed = JSON.parse(txt)
  } catch {
    setMsg('Import data không hợp lệ (JSON parse error).', 'error')
    return
  }
  const selected = (videos.value ?? []).filter((x) => selectedVideoIds.value.has(x.key))
  if (!Array.isArray(parsed) || parsed.length === 0) {
    setMsg('Import data không hợp lệ (cần array JSON).', 'error')
    return
  }
  let filled = 0
  for (let i = 0; i < selected.length && i < parsed.length; i++) {
    const v = selected[i]
    if (!v) continue
    const item = parsed[i]
    if (typeof item === 'object' && item !== null) {
      if (typeof item.title === 'string') v._editTitle = item.title.slice(0, 100)
      if (typeof item.description === 'string') v._editDescription = item.description.slice(0, 5000)
      if (typeof item.tags === 'string') v._editTagsText = item.tags
      else if (Array.isArray(item.tags)) v._editTagsText = item.tags.join(', ')
      if (typeof item.privacyStatus === 'string' && ['public', 'unlisted', 'private'].includes(item.privacyStatus)) {
        v._editPrivacy = item.privacyStatus
      }
      filled += 1
    }
  }
  bulkEditAiImportText.value = ''
  setMsg(`Đã điền ${filled}/${selected.length} video từ import data.`, 'success')
}

const closePlaylistManager = () => {
  playlistManagerOpen.value = false
}

const closePlaylistEdit = () => {
  playlistEditOpen.value = false
}

const loadPlaylistsForBulk = async () => {
  if (!singleVideoChannelId.value) return
  playlistsLoading.value = true
  try {
    const data = await api.get<{ id: string; title: string }[]>(`youtube/channels/${encodeURIComponent(singleVideoChannelId.value)}/playlists`)
    playlists.value = Array.isArray(data) ? data : []
  } catch {
    playlists.value = []
  } finally {
    playlistsLoading.value = false
  }
}

const loadPlaylistsForEdit = async () => {
  if (!singleVideoChannelId.value || !editForm.id) return
  playlistsLoading.value = true
  try {
    const data = await api.get<{ id: string; title: string }[]>(`youtube/channels/${encodeURIComponent(singleVideoChannelId.value)}/playlists`)
    playlists.value = Array.isArray(data) ? data : []
  } catch {
    playlists.value = []
  } finally {
    playlistsLoading.value = false
  }
}

const openPlaylistManager = async () => {
  clearMsg()
  if (!singleVideoChannelId.value) {
    setMsg('Vui lòng chọn đúng 1 kênh để quản lý playlist.', 'error')
    return
  }
  playlistManagerOpen.value = true
  await loadPlaylistsForBulk()
}

const createPlaylist = async () => {
  clearMsg()
  if (!singleVideoChannelId.value) return
  if (!playlistCreateForm.title.trim()) {
    setMsg('Title playlist là bắt buộc.', 'error')
    return
  }

  playlistManagerWorking.value = true
  try {
    await api.post<{ success: boolean; playlistId: string }>(`youtube/channels/${encodeURIComponent(singleVideoChannelId.value)}/playlists`, {
      title: playlistCreateForm.title,
      description: playlistCreateForm.description,
      privacyStatus: playlistCreateForm.privacyStatus,
    })
    playlistCreateForm.title = ''
    playlistCreateForm.description = ''
    playlistCreateForm.privacyStatus = 'public'
    setMsg('Đã tạo playlist.', 'success')
    await loadPlaylistsForBulk()
  } catch (e: any) {
    setMsg(e?.message ?? 'Không tạo được playlist.', 'error')
  } finally {
    playlistManagerWorking.value = false
  }
}

const deletePlaylist = async (playlistId: string) => {
  clearMsg()
  if (!playlistId) return
  playlistManagerWorking.value = true
  try {
    await api.delete<void>(`youtube/playlists/${encodeURIComponent(playlistId)}`)
    setMsg('Đã xoá playlist.', 'success')
    await loadPlaylistsForBulk()
  } catch (e: any) {
    setMsg(e?.message ?? 'Không xoá được playlist.', 'error')
  } finally {
    playlistManagerWorking.value = false
  }
}

const openPlaylistEdit = (p: { id: string; title: string }) => {
  playlistEditForm.id = p.id
  playlistEditForm.title = p.title
  playlistEditForm.description = ''
  playlistEditForm.privacyStatus = 'public'
  playlistEditOpen.value = true
}

const savePlaylistEdit = async () => {
  clearMsg()
  if (!playlistEditForm.id || !playlistEditForm.title.trim()) {
    setMsg('Title playlist là bắt buộc.', 'error')
    return
  }
  playlistManagerWorking.value = true
  try {
    await api.put<void>(`youtube/playlists/${encodeURIComponent(playlistEditForm.id)}`, {
      title: playlistEditForm.title,
      description: playlistEditForm.description,
      privacyStatus: playlistEditForm.privacyStatus,
    })
    setMsg('Đã lưu playlist.', 'success')
    closePlaylistEdit()
    await loadPlaylistsForBulk()
  } catch (e: any) {
    setMsg(e?.message ?? 'Không lưu được playlist.', 'error')
  } finally {
    playlistManagerWorking.value = false
  }
}

const togglePlaylist = async (playlistId: string, checked: boolean) => {
  if (!editForm.id) return
  playlistWorkingId.value = playlistId
  try {
    if (checked) {
      await api.post<void>(`youtube/playlists/${encodeURIComponent(playlistId)}/videos/${encodeURIComponent(editForm.id)}`, {})
      selectedPlaylistIds.value.add(playlistId)
    } else {
      await api.delete<void>(`youtube/playlists/${encodeURIComponent(playlistId)}/videos/${encodeURIComponent(editForm.id)}`)
      selectedPlaylistIds.value.delete(playlistId)
    }
    selectedPlaylistIds.value = new Set(selectedPlaylistIds.value)
  } finally {
    playlistWorkingId.value = null
  }
}

const onPickThumb = (e: Event) => {
  const input = e.target as HTMLInputElement
  const file = input.files?.[0] ?? null
  editForm.thumbFile = file
  editForm.thumbPreview = file ? URL.createObjectURL(file) : ''
}

const saveThumb = async () => {
  clearMsg()
  if (!editForm.id || !editForm.thumbFile) return

  savingThumb.value = true
  workingId.value = `${singleVideoChannelId.value}:${editForm.id}`
  try {
    const form = new FormData()
    form.append('file', editForm.thumbFile)

    await api.post<void>(`youtube/videos/${encodeURIComponent(editForm.id)}/thumbnail`, form, {
      headers: { 'Content-Type': 'multipart/form-data' },
    } as any)

    setMsg('Đã cập nhật thumbnail.', 'success')
    await loadVideos()
  } catch (e: any) {
    setMsg(e?.message ?? 'Không cập nhật được thumbnail.', 'error')
  } finally {
    savingThumb.value = false
    workingId.value = null
  }
}

onMounted(async () => {
  await loadChannels()

  const params = new URLSearchParams(window.location.search)
  const ytConnected = params.get('ytConnected')
  const ytError = params.get('ytError')

  if (ytConnected === '1') {
    setMsg('Kết nối kênh thành công.', 'success')
  }
  if (ytError) {
    setMsg(decodeURIComponent(ytError), 'error')
  }
})
</script>

<style scoped>
.tab-layout {
  display: flex;
  gap: 20px;
  align-items: flex-start;
}

.label-row {
  display: flex;
  justify-content: space-between;
  align-items: baseline;
  gap: 10px;
}

.char-counter {
  font-size: 12px;
  opacity: 0.75;
}

.tab-main {
  flex: 1;
  min-width: 0;
}

.upload-overlay {
  position: fixed;
  inset: 0;
  background: rgba(0, 0, 0, 0.55);
  display: flex;
  align-items: center;
  justify-content: center;
  z-index: 50;
}

.upload-panel {
  width: 720px;
  max-width: calc(100vw - 24px);
  max-height: calc(100vh - 24px);
  overflow: auto;
}

.modal-backdrop .scan-links-modal {
  width: 1280px !important;
  max-width: calc(100vw - 24px) !important;
  max-height: calc(100vh - 24px) !important;
  overflow: hidden;
  display: flex;
  flex-direction: column;
}

.scan-links-modal .table-wrap {
  flex: 1;
  min-height: 0;
  overflow: auto;
}

.scan-toolbar {
  display: flex;
  gap: 12px;
  align-items: flex-end;
  flex-wrap: wrap;
}

.scan-actions {
  display: flex;
  gap: 8px;
  align-items: center;
  flex-wrap: wrap;
}

.scan-video-title {
  font-weight: 600;
}

.scan-video-sub {
  font-size: 12px;
  opacity: 0.75;
}

.badge {
  display: inline-block;
  padding: 3px 8px;
  border-radius: 999px;
  font-size: 12px;
  font-weight: 600;
}

.badge.ok {
  background: #e9f9ee;
  color: #137a2a;
  border: 1px solid #bfeccb;
}

.badge.warn {
  background: #fff7e6;
  color: #8a5a00;
  border: 1px solid #ffe0a3;
}

.mono {
  font-family: ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, 'Liberation Mono', 'Courier New', monospace;
  font-size: 12px;
}

.bulk-upload-panel {
  width: 1280px;
  max-width: calc(100vw - 24px);
  max-height: calc(100vh - 24px);
  overflow: hidden;
  display: flex;
  flex-direction: column;
  --bulk-col-select-min: 44px;
  --bulk-col-select-max: 44px;
  --bulk-col-file-min: 260px;
  --bulk-col-file-max: 260px;
  --bulk-col-status-min: 110px;
  --bulk-col-status-max: 110px;
  --bulk-col-title-min: 260px;
  --bulk-col-title-max: 260px;
  --bulk-col-desc-min: 340px;
  --bulk-col-desc-max: 340px;
  --bulk-col-privacy-min: 140px;
  --bulk-col-privacy-max: 140px;
  --bulk-col-tags-min: 260px;
  --bulk-col-tags-max: 260px;
  --bulk-col-schedule-min: 200px;
  --bulk-col-schedule-max: 200px;
}

.bulk-grid-wrap {
  margin-top: 8px;
  flex: 1;
  min-height: 0;
  overflow: auto;
  width: 100%;
}

.bulk-grid {
  display: grid;
  grid-template-columns:
    minmax(var(--bulk-col-select-min), var(--bulk-col-select-max))
    minmax(var(--bulk-col-file-min), var(--bulk-col-file-max))
    minmax(var(--bulk-col-status-min), var(--bulk-col-status-max))
    minmax(var(--bulk-col-title-min), var(--bulk-col-title-max))
    minmax(var(--bulk-col-desc-min), var(--bulk-col-desc-max))
    minmax(var(--bulk-col-privacy-min), var(--bulk-col-privacy-max))
    minmax(var(--bulk-col-tags-min), var(--bulk-col-tags-max))
    minmax(var(--bulk-col-schedule-min), var(--bulk-col-schedule-max));
  gap: 10px;
  width: max-content;
  align-items: stretch;
}

.bulk-grid,
.bulk-grid * {
  box-sizing: border-box;
}

.bulk-grid.header {
  position: sticky;
  top: 0;
  z-index: 1;
  background: #fff;
  padding: 0;
  border-bottom: 1px solid rgba(0, 0, 0, 0.08);
}

.bulk-grid.row {
  padding: 0;
  border-bottom: 1px solid rgba(0, 0, 0, 0.06);
}

.bulk-grid .cell {
  font-size: 12px;
  padding: 8px 8px;
  display: flex;
  align-items: center;
}

.bulk-grid .cell.select {
  justify-content: center;
}


.bulk-grid .cell > * {
  width: 100%;
}

.bulk-grid.header .cell {
  font-weight: 700;
  color: #444;
  align-items: flex-end;
}

.bulk-grid .cell textarea {
  resize: vertical;
}

.bulk-grid .cell.file {
  font-weight: 600;
  min-width: 200px;
  max-width: 200px;
  overflow: hidden;
  display: block;
}

.bulk-grid .cell.file .file-text {
  display: block;
  width: 100%;
  min-width: 200px;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.bulk-grid .cell.status {
  font-weight: 600;
}

.bulk-grid .cell.status.success {
  color: #1a7f37;
}

.bulk-grid .cell.status.error {
  color: #b42318;
}

.input-wrap {
  position: relative;
  width: 100%;
}

.input-wrap .input,
.input-wrap .textarea {
  padding-right: 52px;
}

.input-wrap .char-counter {
  position: absolute;
  right: 8px;
  bottom: 10px;
  font-size: 11px;
  opacity: 0.75;
  pointer-events: none;
}

@media (max-width: 1024px) {
  .tab-layout {
    flex-direction: column;
  }

  .upload-overlay {
    position: fixed;
    inset: 0;
    background: rgba(0, 0, 0, 0.35);
    z-index: 50;
    display: flex;
    align-items: center;
    justify-content: center;
    padding: 16px;
    flex: 0 0 auto;
  }

  .upload-panel {
    width: 100%;
    max-width: 520px;
    max-height: calc(100vh - 32px);
    overflow: auto;
  }
}

.yt-admin-page {
  padding: 16px;
}

.page-header {
  margin-bottom: 14px;
}

.breadcrumb {
  display: flex;
  align-items: center;
  gap: 8px;
  font-size: 12px;
  color: #666;
}

.sep {
  color: #aaa;
}

.current {
  color: #222;
  font-weight: 600;
}

.header-row {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-top: 10px;
  gap: 12px;
}

.page-title {
  margin: 0;
  font-size: 18px;
  font-weight: 700;
  color: #222;
}

.header-actions {
  display: flex;
  gap: 8px;
}

.tabs {
  display: flex;
  gap: 6px;
  margin-top: 12px;
}

.tab {
  border: 1px solid #eee;
  background: #fff;
  padding: 8px 10px;
  border-radius: 8px;
  font-size: 13px;
  cursor: pointer;
  color: #333;
}

.tab.active {
  border-color: rgba(242, 111, 59, 0.35);
  background: rgba(242, 111, 59, 0.06);
  color: #b54416;
}

.tab:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}

.grid {
  display: grid;
  grid-template-columns: 1fr 360px;
  gap: 12px;
}

.card {
  background: #fff;
  border: 1px solid #eee;
  border-radius: 10px;
  padding: 12px;
}

.card-title {
  font-weight: 700;
  font-size: 13px;
  color: #222;
  margin-bottom: 10px;
}

.muted {
  color: #666;
  font-size: 13px;
}

.table-wrap {
  overflow: auto;
}

.table {
  width: 100%;
  border-collapse: collapse;
  font-size: 13px;
}

.table th,
.table td {
  padding: 10px;
  border-bottom: 1px solid #f1f1f1;
  text-align: left;
  vertical-align: middle;
}

.table th {
  color: #666;
  font-weight: 600;
  background: #fafafa;
}

tr.selected {
  background: rgba(242, 111, 59, 0.04);
}

.channel-cell,
.video-cell {
  display: flex;
  align-items: center;
  gap: 10px;
}

.thumb-actions {
  display: flex;
  gap: 8px;
  align-items: center;
}

.thumb-preview {
  margin-top: 8px;
}

.playlist-list {
  display: grid;
  gap: 8px;
}

.playlist-item {
  display: flex;
  align-items: center;
  gap: 8px;
  font-size: 13px;
  color: #333;
}

.playlist-title {
  line-height: 1.2;
}

.thumb {
  width: 44px;
  height: 44px;
  border-radius: 8px;
  object-fit: cover;
  border: 1px solid #eee;
  background: #fafafa;
}

.channel-title {
  font-weight: 700;
  color: #222;
}

.channel-sub {
  font-size: 12px;
  color: #666;
}

.row-actions {
  display: flex;
  justify-content: flex-end;
  gap: 8px;
}

.badge {
  display: inline-flex;
  align-items: center;
  padding: 4px 8px;
  border-radius: 999px;
  font-size: 12px;
  border: 1px solid #eee;
}

.badge.ok {
  background: #f0fff4;
  border-color: #b7f5c8;
  color: #1a7f3c;
}

.badge.warn {
  background: #fffaf0;
  border-color: #f7e2b3;
  color: #8a5a00;
}

.btn {
  border: 1px solid #f26f3b;
  background: #f26f3b;
  color: #fff;
  border-radius: 8px;
  padding: 8px 10px;
  font-size: 13px;
  cursor: pointer;
}

.btn.secondary {
  background: #fff;
  color: #b54416;
  border-color: rgba(242, 111, 59, 0.35);
}

.btn.tiny {
  padding: 6px 8px;
  border-radius: 8px;
  font-size: 12px;
}

.btn.danger {
  background: #fff;
  border-color: #ffd2d2;
  color: #b42318;
}

.btn:disabled {
  opacity: 0.6;
  cursor: not-allowed;
}

.msg {
  margin: 10px 0;
  padding: 10px 12px;
  border-radius: 10px;
  border: 1px solid #eee;
  font-size: 13px;
}

.msg.success {
  background: #f0fff4;
  border-color: #b7f5c8;
  color: #1a7f3c;
}

.msg.error {
  background: #fff5f5;
  border-color: #ffd2d2;
  color: #b42318;
}

.code {
  font-family: ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, "Liberation Mono", "Courier New", monospace;
  font-size: 12px;
  margin-top: 6px;
  padding: 6px 8px;
  border: 1px solid #eee;
  border-radius: 8px;
  background: #fafafa;
  color: #333;
  word-break: break-all;
}

.video-toolbar {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 10px;
  margin-bottom: 10px;
}

.toolbar-left {
  display: flex;
  align-items: center;
  gap: 10px;
  flex-wrap: wrap;
  flex: 1;
}

.bulk-info {
  display: flex;
  align-items: center;
  gap: 8px;
  flex-wrap: wrap;
  width: 50%;
}

.bulk-left {
  display: flex;
  align-items: center;
  gap: 10px;
}

.bulk-title {
  font-size: 13px;
  font-weight: 700;
  color: #222;
}

.bulk-actions {
  display: flex;
  align-items: center;
  gap: 8px;
}

.video-cards {
  display: grid;
  grid-template-columns: repeat(5, 300px);
  gap: 10px;
}

.video-card {
  border: 1px solid #eee;
  border-radius: 12px;
  background: #fff;
  overflow: hidden;
  display: flex;
  flex-direction: column;
}

.video-card.selected {
  border-color: rgba(242, 111, 59, 0.35);
  box-shadow: 0 0 0 3px rgba(242, 111, 59, 0.06);
}

.video-card-top {
  position: relative;
}

.video-check {
  position: absolute;
  top: 8px;
  left: 8px;
  z-index: 2;
  background: rgba(255, 255, 255, 0.9);
  border: 1px solid #eee;
  padding: 4px 6px;
  border-radius: 8px;
}

.video-thumb-wrap {
  width: 100%;
  aspect-ratio: 16 / 9;
  background: #fafafa;
}

.video-thumb {
  width: 100%;
  height: 100%;
  object-fit: cover;
  display: block;
}

.video-thumb.placeholder {
  width: 100%;
  height: 100%;
}

.video-card-body {
  padding: 10px;
}

.video-title {
  font-weight: 800;
  color: #222;
  font-size: 13px;
  line-height: 1.3;
  display: -webkit-box;
  -webkit-line-clamp: 2;
  -webkit-box-orient: vertical;
  overflow: hidden;
}

.video-sub {
  margin-top: 6px;
  font-size: 12px;
  color: #666;
  word-break: break-all;
}

.video-meta {
  margin-top: 8px;
  display: flex;
  align-items: center;
  gap: 6px;
  color: #666;
  font-size: 12px;
}

.meta-dot {
  color: #aaa;
}

.video-stats {
  margin-top: 10px;
  display: grid;
  grid-template-columns: repeat(4, minmax(0, 1fr));
  gap: 6px;
}

.stat {
  border: 1px solid #f1f1f1;
  border-radius: 10px;
  padding: 6px 8px;
  background: #fafafa;
}

.stat-label {
  display: block;
  font-size: 11px;
  color: #666;
}

.stat-value {
  display: block;
  margin-top: 2px;
  font-size: 13px;
  font-weight: 800;
  color: #222;
}

.video-card-actions {
  display: flex;
  justify-content: flex-end;
  gap: 8px;
  padding: 10px;
  border-top: 1px solid #f1f1f1;
}

.affiliate-picker-modal {
  width: 840px;
  max-width: calc(100vw - 24px);
  max-height: calc(100vh - 24px);
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.picker-toolbar {
  display: flex;
  gap: 8px;
  align-items: center;
}

.affiliate-picker-list {
  display: grid;
  gap: 8px;
  overflow: auto;
  max-height: 52vh;
}

.affiliate-picker-item {
  display: flex;
  align-items: center;
  gap: 10px;
  width: 100%;
  text-align: left;
  border: 1px solid #eee;
  border-radius: 10px;
  background: #fff;
  padding: 10px;
  cursor: pointer;
}

.affiliate-picker-item.active {
  border-color: rgba(242, 111, 59, 0.35);
  box-shadow: 0 0 0 3px rgba(242, 111, 59, 0.06);
}

.affiliate-picker-thumb {
  width: 56px;
  height: 56px;
  border-radius: 10px;
  object-fit: cover;
  border: 1px solid #eee;
  background: #fafafa;
  flex: 0 0 auto;
}

.affiliate-picker-meta {
  min-width: 0;
  flex: 1;
}

.affiliate-picker-name {
  font-weight: 700;
  color: #222;
}

.affiliate-picker-sub {
  margin-top: 4px;
  font-size: 12px;
  color: #666;
}

.affiliate-picker-aff-link {
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.picker-paging {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
}

.paging-actions {
  display: flex;
  align-items: center;
  gap: 8px;
}

.playlist-manager-list {
  display: grid;
  gap: 8px;
}

.playlist-manager-item {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 10px;
  padding: 10px;
  border: 1px solid #eee;
  border-radius: 10px;
  background: #fff;
}

.playlist-manager-main {
  min-width: 0;
}

.playlist-manager-title {
  font-weight: 800;
  color: #222;
  font-size: 13px;
}

.playlist-manager-sub {
  font-size: 12px;
  color: #666;
  margin-top: 4px;
  word-break: break-all;
}

.playlist-manager-actions {
  display: flex;
  align-items: center;
  gap: 8px;
}

.row {
  display: flex;
  align-items: center;
}

.toolbar-actions {
  display: flex;
  gap: 8px;
}

.view-toggle {
  display: flex;
  border: 1px solid rgba(242, 111, 59, 0.35);
  border-radius: 10px;
  overflow: hidden;
  background: #fff;
}

.toggle {
  border: 0;
  background: transparent;
  padding: 8px 10px;
  font-size: 13px;
  cursor: pointer;
  color: #b54416;
}

.toggle.active {
  background: rgba(242, 111, 59, 0.08);
  font-weight: 700;
}

.input,
.textarea {
  width: 100%;
  border: 1px solid #eee;
  border-radius: 8px;
  padding: 8px 10px;
  font-size: 13px;
  outline: none;
}

.input:focus,
.textarea:focus {
  border-color: rgba(242, 111, 59, 0.45);
}

.field label {
  display: block;
  font-size: 12px;
  color: #666;
  margin-bottom: 6px;
}

.upload-fab {
  position: absolute;
  right: 14px;
  bottom: 14px;
  display: flex;
  align-items: center;
  gap: 8px;
  background: #f26f3b;
  color: #fff;
  border: none;
  border-radius: 999px;
  padding: 12px 20px;
  font-size: 14px;
  font-weight: 700;
  cursor: pointer;
  box-shadow: 0 4px 12px rgba(242, 111, 59, 0.3);
  transition: all 0.2s ease;
  z-index: 40;
}

.upload-fab.bulk {
  bottom: 68px;
}

.upload-fab:hover {
  background: #e55a2b;
  transform: translateY(-2px);
  box-shadow: 0 6px 16px rgba(242, 111, 59, 0.35);
}

.upload-fab:disabled {
  background: #ccc;
  cursor: not-allowed;
  box-shadow: none;
  transform: none;
}

.fab-icon {
  font-size: 20px;
  line-height: 1;
}

.fab-label {
  font-size: 14px;
}

.sidebar-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 16px 20px;
  border-bottom: 1px solid #eee;
}

.sidebar-title {
  font-size: 16px;
  font-weight: 700;
  color: #222;
}

.sidebar-close {
  background: none;
  border: none;
  font-size: 20px;
  color: #999;
  cursor: pointer;
  padding: 4px;
  border-radius: 6px;
  transition: transform 0.16s ease;
}

.sidebar-close:hover {
  background: #f3f3f3;
  color: #333;
}

.sidebar-content {
  flex: 1;
  padding: 20px;
  overflow-y: auto;
}

.bulk-upload-panel .sidebar-content {
  overflow: hidden;
  display: flex;
  flex-direction: column;
}

.bulk-upload-panel .sidebar-content .form {
  flex: 1;
  min-height: 0;
  overflow: hidden;
  display: flex;
  flex-direction: column;
}

@media (max-width: 640px) {
  .upload-fab {
    bottom: 16px;
    right: 16px;
    padding: 10px 16px;
    font-size: 13px;
  }

  .upload-fab.bulk {
    bottom: 64px;
  }

  .fab-icon {
    font-size: 18px;
  }

  .fab-label {
    display: none;
  }

  .right-sidebar {
    width: 100vw;
    max-width: 100vw;
  }
}

.modal-backdrop {
  position: fixed;
  inset: 0;
  background: rgba(0, 0, 0, 0.35);
  display: flex;
  align-items: center;
  justify-content: center;
  padding: 16px;
  z-index: 50;
}

.modal {
  width: 100%;
  max-width: 520px;
  background: #fff;
  border-radius: 12px;
  border: 1px solid #eee;
  padding: 14px;
}

.modal-title {
  font-size: 14px;
  font-weight: 700;
  margin-bottom: 12px;
  color: #222;
}

.form {
  display: flex;
  flex-direction: column;
  gap: 10px;
}

.actions {
  display: flex;
  gap: 8px;
  align-items: center;
}

.progress {
  height: 8px;
  border-radius: 999px;
  background: #f3f3f3;
  overflow: hidden;
  border: 1px solid #eee;
}

.bar {
  height: 100%;
  background: #f26f3b;
  width: 0;
}

.analytics-grid {
  display: grid;
  grid-template-columns: repeat(4, minmax(0, 1fr));
  gap: 10px;
}

.metric {
  border: 1px solid #eee;
  border-radius: 10px;
  padding: 10px;
  background: #fafafa;
}

.metric-label {
  font-size: 12px;
  color: #666;
}

.metric-value {
  margin-top: 6px;
  font-size: 18px;
  font-weight: 800;
  color: #222;
}

@media (max-width: 1080px) {
  .grid {
    grid-template-columns: 1fr;
  }

  .analytics-grid {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }

  .video-cards {
    grid-template-columns: repeat(3, 300px);
  }
}

@media (max-width: 720px) {
  .video-cards {
    grid-template-columns: repeat(2, 300px);
  }
}

@media (max-width: 480px) {
  .video-cards {
    grid-template-columns: 1fr;
  }
}
</style>
