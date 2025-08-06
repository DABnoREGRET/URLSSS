<script setup>
import { ref, computed, onMounted } from 'vue'
import axios from 'axios'

// Reactive state
const longUrl = ref('')
const customAlias = ref('')
const shortenedUrl = ref('')
const isLoading = ref(false)
const showResult = ref(false)
const urlHistory = ref([])
const stats = ref({
  totalShortened: 0,
  totalClicks: 0,
  averageLength: 0
})
const activeTab = ref('shorten')

// Computed properties
const isValidUrl = computed(() => {
  try {
    new URL(longUrl.value)
    return true
  } catch {
    return false
  }
})

const customUrlPreview = computed(() => {
  if (!customAlias.value.trim()) return null
  return `http://localhost:5001/${customAlias.value.trim()}`
})

const originalUrlLength = computed(() => longUrl.value.length)
const shortenedUrlLength = computed(() => shortenedUrl.value.length)
const lengthSaved = computed(() => originalUrlLength.value - shortenedUrlLength.value)

// Methods
const shortenUrl = async () => {
  if (!longUrl.value.trim()) {
    showNotification('Please enter a URL to shorten', 'error')
    return
  }

  if (!isValidUrl.value) {
    showNotification('Please enter a valid URL', 'error')
    return
  }

  isLoading.value = true
  showResult.value = false

  try {
    const response = await axios.post('/api/urls/shorten', {
      originalUrl: longUrl.value,
      customShortCode: customAlias.value.trim() || null,
      expirationDate: null
    }, {
      headers: {
        'Content-Type': 'application/json'
      }
    })
    
    shortenedUrl.value = response.data.shortenedUrl
    
    // Add to history
    urlHistory.value.unshift({
      original: longUrl.value,
      shortened: shortenedUrl.value,
      timestamp: new Date().toISOString(),
      customAlias: customAlias.value.trim() || null
    })
    
    // Keep only last 10 entries
    if (urlHistory.value.length > 10) {
      urlHistory.value = urlHistory.value.slice(0, 10)
    }
    
    showResult.value = true
    updateStats()
    showNotification('URL shortened successfully!', 'success')
  } catch (error) {
    console.error('Error shortening URL:', error)
    let errorMessage = 'An unexpected error occurred. Please try again.'
    
    if (error.response) {
      errorMessage = error.response.data?.error || error.response.data?.message || 'Server error occurred'
    } else if (error.request) {
      errorMessage = 'Failed to connect to server. Please make sure the backend is running.'
    }
    
    showNotification(`Failed to shorten URL: ${errorMessage}`, 'error')
  } finally {
    isLoading.value = false
  }
}

const copyToClipboard = async () => {
  try {
    await navigator.clipboard.writeText(shortenedUrl.value)
    showNotification('Shortened URL copied to clipboard!', 'success')
  } catch (error) {
    console.error('Failed to copy to clipboard:', error)
    showNotification('Failed to copy to clipboard', 'error')
  }
}

const clearForm = () => {
  longUrl.value = ''
  customAlias.value = ''
  shortenedUrl.value = ''
  showResult.value = false
}

const showNotification = (message, type = 'info') => {
  // Simple notification system
  const notification = document.createElement('div')
  notification.className = `notification notification-${type}`
  notification.textContent = message
  document.body.appendChild(notification)
  
  setTimeout(() => {
    notification.remove()
  }, 3000)
}

const updateStats = () => {
  stats.value.totalShortened = urlHistory.value.length
  stats.value.totalClicks = urlHistory.value.length * Math.floor(Math.random() * 100) + 10 // Mock data
  stats.value.averageLength = urlHistory.value.length > 0 
    ? Math.round(urlHistory.value.reduce((sum, url) => sum + url.original.length, 0) / urlHistory.value.length)
    : 0
}

const formatDate = (dateString) => {
  return new Date(dateString).toLocaleDateString('en-US', {
    month: 'short',
    day: 'numeric',
    hour: '2-digit',
    minute: '2-digit'
  })
}

const getUrlDomain = (url) => {
  try {
    return new URL(url).hostname
  } catch {
    return 'Invalid URL'
  }
}

const extractShortCode = (shortenedUrl) => {
  try {
    const url = new URL(shortenedUrl)
    return url.pathname.substring(1) // Remove leading slash
  } catch {
    // Fallback: extract from the end of the URL
    return shortenedUrl.split('/').pop()
  }
}

const deleteUrlFromBackend = async (shortCode) => {
  try {
    const response = await axios.delete(`/api/urls/${shortCode}`)
    return response.status === 200
  } catch (error) {
    console.error(`Error deleting URL ${shortCode} from backend:`, error)
    return false
  }
}

const deleteUrl = async (index) => {
  const item = urlHistory.value[index]
  const shortCode = extractShortCode(item.shortened)
  
  try {
    confirm("Do you want to delete?")
    // First remove from local history immediately for better UX
    urlHistory.value.splice(index, 1)
    updateStats()
    
    // Then try to delete from backend (non-blocking)
    const backendSuccess = await deleteUrlFromBackend(shortCode)
    if (backendSuccess) {
      showNotification('URL deleted successfully', 'success')
    } else {
      showNotification('URL removed from history but may not be deleted from server', 'warning')
    }
  } catch (error) {
    console.error('Error in delete operation:', error)
    showNotification('Failed to delete URL', 'error')
  }
}

const deleteAllUrls = async () => {
  if (urlHistory.value.length === 0) {
    showNotification('No URLs to delete', 'info')
    return
  }
  
  if (confirm('Are you sure you want to delete all URLs from history? This action cannot be undone.')) {
    try {
      // First clear local history immediately for better UX
      const urlsToDelete = [...urlHistory.value]
      urlHistory.value = []
      updateStats()
      
      // Then try to delete from backend (non-blocking)
      const deletePromises = urlsToDelete.map(async (item) => {
        const shortCode = extractShortCode(item.shortened)
        return await deleteUrlFromBackend(shortCode)
      })
      
      // Run deletions in background
      Promise.all(deletePromises).then(() => {
        showNotification('All URLs deleted successfully', 'success')
      }).catch(() => {
        showNotification('URLs removed from history but some may not be deleted from server', 'warning')
      })
      
    } catch (error) {
      console.error('Error in delete all operation:', error)
      showNotification('Failed to delete all URLs', 'error')
    }
  }
}

onMounted(() => {
  // Load history from localStorage
  const savedHistory = localStorage.getItem('urlsss-history')
  if (savedHistory) {
    urlHistory.value = JSON.parse(savedHistory)
  }
  
  updateStats()
})

// Save history to localStorage whenever it changes
const saveHistory = () => {
  localStorage.setItem('urlsss-history', JSON.stringify(urlHistory.value))
}

// Watch for changes in history
import { watch } from 'vue'
watch(urlHistory, saveHistory, { deep: true })
</script>

<template>
  <div class="app">
    <!-- Header -->
    <header class="header">
      <div class="header-content">
        <div class="logo">
          <div class="logo-icon">🔗</div>
          <span class="logo-text">URLSSS</span>
          <span class="logo-tagline">Super Simple URL Shortener</span>
        </div>
        
        <nav class="nav">
          <button 
            @click="activeTab = 'shorten'"
            :class="['nav-btn', { active: activeTab === 'shorten' }]"
          >
            Shorten
          </button>
          <button 
            @click="activeTab = 'history'"
            :class="['nav-btn', { active: activeTab === 'history' }]"
          >
            History
          </button>
          <button 
            @click="activeTab = 'stats'"
            :class="['nav-btn', { active: activeTab === 'stats' }]"
          >
            Stats
          </button>
        </nav>
      </div>
    </header>

    <!-- Main Content -->
    <main class="main">
      <!-- Shorten Tab -->
      <div v-if="activeTab === 'shorten'" class="tab-content">
        <div class="hero-section">
          <h1 class="hero-title">Shorten Your URLs with URLSSS</h1>
          <p class="hero-subtitle">Transform long URLs into short, shareable links instantly</p>

          <div class="url-shortener">
            <div class="input-group">
              <div class="url-input-container">
                <label class="input-label">Enter your long URL</label>
                <input
                  v-model="longUrl"
                  type="url"
                  placeholder="https://example.com/very-long-url-that-needs-shortening"
                  class="url-input"
                  @keyup.enter="shortenUrl"
                  :disabled="isLoading"
                  :class="{ 'error': longUrl && !isValidUrl }"
                />
                <div v-if="longUrl && !isValidUrl" class="error-message">
                  Please enter a valid URL starting with http:// or https://
                </div>
              </div>
              
              <div class="alias-input-container">
                <label class="input-label">Custom alias (optional)</label>
                <input
                  v-model="customAlias"
                  type="text"
                  placeholder="my-custom-link"
                  class="alias-input"
                  @keyup.enter="shortenUrl"
                  :disabled="isLoading"
                />
                <div v-if="customUrlPreview" class="alias-preview">
                  <span class="preview-text">Preview: </span>
                  <span class="preview-url">{{ customUrlPreview }}</span>
                </div>
              </div>
            </div>
            
            <div class="button-group">
              <button 
                @click="shortenUrl"
                :disabled="isLoading || !longUrl.trim()"
                class="shorten-btn"
              >
                <span v-if="!isLoading">🔗 Shorten URL</span>
                <div v-else class="loading-spinner"></div>
              </button>
              <button 
                @click="clearForm"
                class="clear-btn"
                :disabled="isLoading"
              >
                Clear
              </button>
            </div>

            <!-- Result Box -->
            <transition name="slide-down">
              <div v-if="showResult && shortenedUrl" class="result-box">
                <div class="result-header">
                  <h3>✅ URL Shortened Successfully!</h3>
                  <div class="length-saved">
                    <span class="saved-text">Length saved:</span>
                    <span class="saved-number">{{ lengthSaved }} characters</span>
                  </div>
                </div>
                
                <div class="result-content">
                  <div class="url-comparison">
                    <div class="original-url">
                      <label>Original URL:</label>
                      <div class="url-display original">
                        <span class="url-text">{{ longUrl }}</span>
                        <span class="url-length">({{ originalUrlLength }} chars)</span>
                      </div>
                    </div>
                    
                    <div class="shortened-url">
                      <label>Shortened URL:</label>
                      <div class="url-display shortened">
                        <input 
                          :value="shortenedUrl" 
                          readonly 
                          class="result-url"
                          @click="$event.target.select()"
                        />
                        <button @click="copyToClipboard" class="copy-btn" title="Copy to clipboard">
                          📋
                        </button>
                      </div>
                    </div>
                  </div>
                  
                  <div class="url-info">
                    <div class="info-item">
                      <span class="info-label">Domain:</span>
                      <span class="info-value">{{ getUrlDomain(longUrl) }}</span>
                    </div>
                    <div class="info-item">
                      <span class="info-label">Shortened:</span>
                      <span class="info-value">{{ getUrlDomain(shortenedUrl) }}</span>
                    </div>
                  </div>
                </div>
              </div>
            </transition>
          </div>
        </div>
      </div>

      <!-- History Tab -->
      <div v-if="activeTab === 'history'" class="tab-content">
        <div class="history-section">
          <div class="history-header-section">
            <div>
              <h2 class="section-title">URL History</h2>
              <p class="section-subtitle">Your recently shortened URLs</p>
            </div>
            <div v-if="urlHistory.length > 0" class="history-actions-header">
              <button @click="deleteAllUrls" class="delete-all-btn">
                🗑️ Delete All
              </button>
            </div>
          </div>
          
          <div v-if="urlHistory.length === 0" class="empty-state">
            <div class="empty-icon">📝</div>
            <h3>No URLs shortened yet</h3>
            <p>Start by shortening your first URL in the Shorten tab!</p>
          </div>
          
          <div v-else class="history-list">
            <div 
              v-for="(item, index) in urlHistory" 
              :key="index"
              class="history-item"
            >
              <div class="history-header">
                <span class="history-date">{{ formatDate(item.timestamp) }}</span>
                <span v-if="item.customAlias" class="custom-badge">Custom</span>
              </div>
              
              <div class="history-urls">
                <div class="history-original">
                  <label>Original:</label>
                  <div class="url-text">{{ item.original }}</div>
                </div>
                <div class="history-shortened">
                  <label>Shortened:</label>
                  <div class="url-text shortened">{{ item.shortened }}</div>
                </div>
              </div>
              
              <div class="history-actions">
                <button @click="() => { shortenedUrl = item.shortened; showResult = true; activeTab = 'shorten' }" class="action-btn">
                  View
                </button>
                <button @click="() => { navigator.clipboard.writeText(item.shortened); showNotification('URL copied to clipboard!', 'success') }" class="action-btn">
                  Copy
                </button>
                <button @click="deleteUrl(index)" class="action-btn delete-btn">
                  🗑️ Delete
                </button>
              </div>
            </div>
          </div>
        </div>
      </div>

      <!-- Stats Tab -->
      <div v-if="activeTab === 'stats'" class="tab-content">
        <div class="stats-section">
          <h2 class="section-title">Your Statistics</h2>
          <p class="section-subtitle">Track your URL shortening activity</p>
          
          <div class="stats-grid">
            <div class="stat-card">
              <div class="stat-icon">🔗</div>
              <div class="stat-content">
                <div class="stat-number">{{ stats.totalShortened }}</div>
                <div class="stat-label">URLs Shortened</div>
              </div>
            </div>
            
            <div class="stat-card">
              <div class="stat-icon">👆</div>
              <div class="stat-content">
                <div class="stat-number">{{ stats.totalClicks }}</div>
                <div class="stat-label">Total Clicks</div>
              </div>
            </div>
            
            <div class="stat-card">
              <div class="stat-icon">📏</div>
              <div class="stat-content">
                <div class="stat-number">{{ stats.averageLength }}</div>
                <div class="stat-label">Avg URL Length</div>
              </div>
            </div>
            
            <div class="stat-card">
              <div class="stat-icon">💾</div>
              <div class="stat-content">
                <div class="stat-number">{{ urlHistory.filter(url => url.customAlias).length }}</div>
                <div class="stat-label">Custom Aliases</div>
              </div>
            </div>
          </div>
          
          <div class="features-section">
            <h3>URLSSS Features</h3>
            <div class="features-grid">
              <div class="feature-item">
                <div class="feature-icon">⚡</div>
                <div class="feature-content">
                  <h4>Lightning Fast</h4>
                  <p>Instant URL shortening with real-time validation</p>
                </div>
              </div>
              
              <div class="feature-item">
                <div class="feature-icon">🎯</div>
                <div class="feature-content">
                  <h4>Custom Aliases</h4>
                  <p>Create memorable, custom short URLs</p>
                </div>
              </div>
              
              <div class="feature-item">
                <div class="feature-icon">📊</div>
                <div class="feature-content">
                  <h4>Analytics</h4>
                  <p>Track your shortened URLs and usage statistics</p>
                </div>
              </div>
              
              <div class="feature-item">
                <div class="feature-icon">🔒</div>
                <div class="feature-content">
                  <h4>Secure</h4>
                  <p>HTTPS-only URLs with safe redirect handling</p>
                </div>
              </div>
            </div>
          </div>
        </div>
      </div>
    </main>

    <!-- Footer -->
    <footer class="footer">
      <div class="footer-content">
        <p>&copy; 2024 URLSSS - Super Simple URL Shortener</p>
        <p>Built with ❤️ using Vue.js and .NET</p>
      </div>
    </footer>
  </div>
</template>

<style scoped>
@import url('https://fonts.googleapis.com/css2?family=Inter:wght@300;400;500;600;700;800;900&display=swap');

/* Global Styles */
* {
  box-sizing: border-box;
  margin: 0;
  padding: 0;
}

.app {
  min-height: 100vh;
  background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
  font-family: 'Inter', -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;
  color: #1a1a1a;
  position: relative;
  overflow-x: hidden;
}

/* Header */
.header {
  background: rgba(255, 255, 255, 0.95);
  backdrop-filter: blur(10px);
  border-bottom: 1px solid rgba(0, 0, 0, 0.1);
  position: sticky;
  top: 0;
  z-index: 100;
}

.header-content {
  max-width: 1200px;
  margin: 0 auto;
  padding: 1rem 2rem;
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 2rem;
}

.logo {
  display: flex;
  align-items: center;
  gap: 0.75rem;
}

.logo-icon {
  font-size: 2rem;
  background: linear-gradient(135deg, #667eea, #764ba2);
  -webkit-background-clip: text;
  -webkit-text-fill-color: transparent;
  background-clip: text;
}

.logo-text {
  font-size: 2rem;
  font-weight: 800;
  background: linear-gradient(135deg, #667eea, #764ba2);
  -webkit-background-clip: text;
  -webkit-text-fill-color: transparent;
  background-clip: text;
  letter-spacing: -0.5px;
}

.logo-tagline {
  font-size: 0.875rem;
  color: #6b7280;
  font-weight: 400;
}

.nav {
  display: flex;
  gap: 0.5rem;
}

.nav-btn {
  padding: 0.75rem 1.5rem;
  border: none;
  border-radius: 12px;
  background: transparent;
  color: #6b7280;
  font-weight: 500;
  cursor: pointer;
  transition: all 0.3s ease;
  font-size: 0.875rem;
}

.nav-btn:hover {
  background: rgba(102, 126, 234, 0.1);
  color: #667eea;
}

.nav-btn.active {
  background: #667eea;
  color: white;
}


/* Main Content */
.main {
  max-width: 1200px;
  margin: 0 auto;
  padding: 2rem;
  min-height: calc(100vh - 200px);
}

.tab-content {
  background: rgba(255, 255, 255, 0.95);
  backdrop-filter: blur(10px);
  border-radius: 24px;
  padding: 3rem;
  box-shadow: 0 20px 40px rgba(0, 0, 0, 0.1);
  border: 1px solid rgba(255, 255, 255, 0.2);
}

/* Hero Section */
.hero-section {
  text-align: center;
}

.hero-title {
  font-size: 3.5rem;
  font-weight: 900;
  margin-bottom: 1rem;
  background: linear-gradient(135deg, #667eea, #764ba2);
  -webkit-background-clip: text;
  -webkit-text-fill-color: transparent;
  background-clip: text;
  line-height: 1.1;
}

.hero-subtitle {
  font-size: 1.25rem;
  color: #6b7280;
  margin-bottom: 3rem;
  font-weight: 400;
}

/* URL Shortener */
.url-shortener {
  max-width: 600px;
  margin: 0 auto;
}

.input-group {
  display: flex;
  flex-direction: column;
  gap: 1.5rem;
  margin-bottom: 2rem;
}

.url-input-container,
.alias-input-container {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
}

.input-label {
  font-weight: 600;
  color: #374151;
  font-size: 0.875rem;
}

.url-input,
.alias-input {
  padding: 1rem 1.5rem;
  font-size: 1rem;
  border: 2px solid #e5e7eb;
  border-radius: 12px;
  background: white;
  color: #1a1a1a;
  transition: all 0.3s ease;
  outline: none;
  font-family: 'Inter', sans-serif;
}

.url-input:focus,
.alias-input:focus {
  border-color: #667eea;
  box-shadow: 0 0 0 3px rgba(102, 126, 234, 0.1);
}

.url-input.error {
  border-color: #ef4444;
}

.url-input:disabled,
.alias-input:disabled {
  opacity: 0.6;
  cursor: not-allowed;
}

.url-input::placeholder,
.alias-input::placeholder {
  color: #9ca3af;
}

.error-message {
  color: #ef4444;
  font-size: 0.875rem;
  margin-top: 0.25rem;
}

.alias-preview {
  padding: 0.75rem 1rem;
  background: rgba(102, 126, 234, 0.1);
  border: 1px solid rgba(102, 126, 234, 0.2);
  border-radius: 8px;
  font-size: 0.875rem;
}

.preview-text {
  color: #6b7280;
}

.preview-url {
  color: #667eea;
  font-weight: 500;
  font-family: 'Inter', monospace;
}

.button-group {
  display: flex;
  gap: 1rem;
  justify-content: center;
}

.shorten-btn {
  padding: 1rem 2rem;
  font-size: 1rem;
  font-weight: 600;
  background: linear-gradient(135deg, #667eea, #764ba2);
  color: white;
  border: none;
  border-radius: 12px;
  cursor: pointer;
  transition: all 0.3s ease;
  min-width: 160px;
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 0.5rem;
}

.shorten-btn:hover:not(:disabled) {
  transform: translateY(-2px);
  box-shadow: 0 8px 25px rgba(102, 126, 234, 0.3);
}

.shorten-btn:disabled {
  opacity: 0.6;
  cursor: not-allowed;
  transform: none;
}

.clear-btn {
  padding: 1rem 2rem;
  font-size: 1rem;
  font-weight: 500;
  background: #f3f4f6;
  color: #6b7280;
  border: 1px solid #d1d5db;
  border-radius: 12px;
  cursor: pointer;
  transition: all 0.3s ease;
}

.clear-btn:hover:not(:disabled) {
  background: #e5e7eb;
}

.loading-spinner {
  width: 20px;
  height: 20px;
  border: 2px solid rgba(255, 255, 255, 0.3);
  border-top: 2px solid white;
  border-radius: 50%;
  animation: spin 1s linear infinite;
}

@keyframes spin {
  to {
    transform: rotate(360deg);
  }
}

/* Result Box */
.result-box {
  background: #f8fafc;
  border: 1px solid #e2e8f0;
  border-radius: 16px;
  padding: 2rem;
  margin-top: 2rem;
}

.result-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 1.5rem;
  padding-bottom: 1rem;
  border-bottom: 1px solid #e2e8f0;
}

.result-header h3 {
  color: #059669;
  font-weight: 600;
}

.length-saved {
  display: flex;
  align-items: center;
  gap: 0.5rem;
}

.saved-text {
  color: #6b7280;
  font-size: 0.875rem;
}

.saved-number {
  background: #059669;
  color: white;
  padding: 0.25rem 0.75rem;
  border-radius: 20px;
  font-size: 0.875rem;
  font-weight: 600;
}

.url-comparison {
  display: flex;
  flex-direction: column;
  gap: 1.5rem;
  margin-bottom: 1.5rem;
}

.original-url,
.shortened-url {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
}

.original-url label,
.shortened-url label {
  font-weight: 600;
  color: #374151;
  font-size: 0.875rem;
}

.url-display {
  display: flex;
  align-items: center;
  gap: 1rem;
  padding: 1rem;
  border-radius: 8px;
  background: white;
  border: 1px solid #e2e8f0;
}

.url-display.original {
  background: #fef3c7;
  border-color: #f59e0b;
}

.url-display.shortened {
  background: #d1fae5;
  border-color: #10b981;
}

.url-text {
  flex: 1;
  font-family: 'Inter', monospace;
  font-size: 0.875rem;
  word-break: break-all;
}

.url-length {
  color: #6b7280;
  font-size: 0.75rem;
  white-space: nowrap;
}

.result-url {
  flex: 1;
  padding: 0.75rem;
  font-size: 0.875rem;
  border: none;
  background: transparent;
  color: #059669;
  font-weight: 500;
  outline: none;
  cursor: pointer;
  font-family: 'Inter', monospace;
}

.copy-btn {
  padding: 0.5rem;
  background: #667eea;
  color: white;
  border: none;
  border-radius: 6px;
  cursor: pointer;
  transition: all 0.3s ease;
  font-size: 1rem;
}

.copy-btn:hover {
  background: #5a67d8;
  transform: scale(1.05);
}

.url-info {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 1rem;
  padding-top: 1rem;
  border-top: 1px solid #e2e8f0;
}

.info-item {
  display: flex;
  justify-content: space-between;
  align-items: center;
}

.info-label {
  font-weight: 500;
  color: #6b7280;
  font-size: 0.875rem;
}

.info-value {
  font-weight: 600;
  color: #374151;
  font-size: 0.875rem;
}

/* History Section */
.history-section {
  text-align: center;
}

.history-header-section {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  margin-bottom: 2rem;
  text-align: left;
}

.history-actions-header {
  display: flex;
  gap: 1rem;
}

.delete-all-btn {
  padding: 0.75rem 1.5rem;
  background: #ef4444;
  color: white;
  border: none;
  border-radius: 8px;
  cursor: pointer;
  transition: all 0.3s ease;
  font-weight: 500;
  font-size: 0.875rem;
  display: flex;
  align-items: center;
  gap: 0.5rem;
}

.delete-all-btn:hover {
  background: #dc2626;
  transform: translateY(-1px);
  box-shadow: 0 4px 12px rgba(239, 68, 68, 0.3);
}

.section-title {
  font-size: 2.5rem;
  font-weight: 800;
  margin-bottom: 1rem;
  background: linear-gradient(135deg, #667eea, #764ba2);
  -webkit-background-clip: text;
  -webkit-text-fill-color: transparent;
  background-clip: text;
}

.section-subtitle {
  font-size: 1.125rem;
  color: #6b7280;
  margin-bottom: 3rem;
}

.empty-state {
  text-align: center;
  padding: 4rem 2rem;
  color: #6b7280;
}

.empty-icon {
  font-size: 4rem;
  margin-bottom: 1rem;
}

.empty-state h3 {
  font-size: 1.5rem;
  font-weight: 600;
  margin-bottom: 0.5rem;
  color: #374151;
}

.history-list {
  display: flex;
  flex-direction: column;
  gap: 1.5rem;
  max-width: 800px;
  margin: 0 auto;
}

.history-item {
  background: white;
  border: 1px solid #e2e8f0;
  border-radius: 12px;
  padding: 1.5rem;
  transition: all 0.3s ease;
}

.history-item:hover {
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.1);
  transform: translateY(-2px);
}

.history-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 1rem;
  padding-bottom: 0.75rem;
  border-bottom: 1px solid #f3f4f6;
}

.history-date {
  color: #6b7280;
  font-size: 0.875rem;
  font-weight: 500;
}

.custom-badge {
  background: #667eea;
  color: white;
  padding: 0.25rem 0.75rem;
  border-radius: 20px;
  font-size: 0.75rem;
  font-weight: 600;
}

.history-urls {
  display: flex;
  flex-direction: column;
  gap: 1rem;
  margin-bottom: 1rem;
}

.history-original,
.history-shortened {
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
}

.history-original label,
.history-shortened label {
  font-weight: 600;
  color: #374151;
  font-size: 0.875rem;
}

.url-text {
  font-family: 'Inter', monospace;
  font-size: 0.875rem;
  color: #6b7280;
  word-break: break-all;
}

.url-text.shortened {
  color: #059669;
  font-weight: 500;
}

.history-actions {
  display: flex;
  gap: 0.5rem;
}

.action-btn {
  padding: 0.5rem 1rem;
  border: 1px solid #d1d5db;
  background: white;
  color: #6b7280;
  border-radius: 6px;
  cursor: pointer;
  transition: all 0.3s ease;
  font-size: 0.875rem;
  font-weight: 500;
}

.action-btn:hover {
  background: #f3f4f6;
  border-color: #9ca3af;
}

.action-btn.delete-btn {
  background: #fef2f2;
  border-color: #fecaca;
  color: #dc2626;
}

.action-btn.delete-btn:hover {
  background: #fee2e2;
  border-color: #fca5a5;
  color: #b91c1c;
}

/* Stats Section */
.stats-section {
  text-align: center;
}

.stats-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
  gap: 1.5rem;
  margin-bottom: 3rem;
}

.stat-card {
  background: white;
  border: 1px solid #e2e8f0;
  border-radius: 12px;
  padding: 2rem;
  display: flex;
  align-items: center;
  gap: 1rem;
  transition: all 0.3s ease;
}

.stat-card:hover {
  transform: translateY(-4px);
  box-shadow: 0 8px 25px rgba(0, 0, 0, 0.1);
}

.stat-icon {
  font-size: 2rem;
  background: linear-gradient(135deg, #667eea, #764ba2);
  -webkit-background-clip: text;
  -webkit-text-fill-color: transparent;
  background-clip: text;
}

.stat-content {
  flex: 1;
  text-align: left;
}

.stat-number {
  font-size: 2rem;
  font-weight: 800;
  color: #1a1a1a;
  line-height: 1;
}

.stat-label {
  font-size: 0.875rem;
  color: #6b7280;
  font-weight: 500;
  margin-top: 0.25rem;
}

.features-section {
  text-align: left;
  max-width: 800px;
  margin: 0 auto;
}

.features-section h3 {
  font-size: 1.5rem;
  font-weight: 700;
  margin-bottom: 2rem;
  color: #1a1a1a;
  text-align: center;
}

.features-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(300px, 1fr));
  gap: 1.5rem;
}

.feature-item {
  display: flex;
  align-items: flex-start;
  gap: 1rem;
  padding: 1.5rem;
  background: white;
  border: 1px solid #e2e8f0;
  border-radius: 12px;
  transition: all 0.3s ease;
}

.feature-item:hover {
  transform: translateY(-2px);
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.1);
}

.feature-icon {
  font-size: 1.5rem;
  background: linear-gradient(135deg, #667eea, #764ba2);
  -webkit-background-clip: text;
  -webkit-text-fill-color: transparent;
  background-clip: text;
  flex-shrink: 0;
}

.feature-content h4 {
  font-size: 1.125rem;
  font-weight: 600;
  color: #1a1a1a;
  margin-bottom: 0.5rem;
}

.feature-content p {
  color: #6b7280;
  font-size: 0.875rem;
  line-height: 1.5;
}

/* Footer */
.footer {
  background: rgba(255, 255, 255, 0.95);
  backdrop-filter: blur(10px);
  border-top: 1px solid rgba(0, 0, 0, 0.1);
  padding: 2rem;
  text-align: center;
}

.footer-content {
  max-width: 1200px;
  margin: 0 auto;
  color: #6b7280;
}

.footer-content p {
  margin-bottom: 0.5rem;
}

.footer-content p:last-child {
  margin-bottom: 0;
}

/* Transitions */
.slide-down-enter-active {
  transition: all 0.4s ease-out;
}

.slide-down-leave-active {
  transition: all 0.3s ease-in;
}

.slide-down-enter-from {
  opacity: 0;
  transform: translateY(-20px);
}

.slide-down-leave-to {
  opacity: 0;
  transform: translateY(-10px);
}

/* Responsive Design */
@media (max-width: 768px) {
  .header-content {
    flex-direction: column;
    gap: 1rem;
    padding: 1rem;
  }
  
  .nav {
    order: 2;
  }
  
  .logo {
    order: 1;
  }
  
  .github-link {
    order: 3;
  }
  
  .main {
    padding: 1rem;
  }
  
  .tab-content {
    padding: 2rem 1.5rem;
  }
  
  .hero-title {
    font-size: 2.5rem;
  }
  
  .section-title {
    font-size: 2rem;
  }
  
  .stats-grid {
    grid-template-columns: 1fr;
  }
  
  .features-grid {
    grid-template-columns: 1fr;
  }
  
  .url-info {
    grid-template-columns: 1fr;
  }
  
  .button-group {
    flex-direction: column;
  }
  
  .shorten-btn,
  .clear-btn {
    width: 100%;
  }
  
  .history-header-section {
    flex-direction: column;
    gap: 1rem;
    text-align: center;
  }
  
  .history-actions-header {
    justify-content: center;
  }
}

@media (max-width: 480px) {
  .hero-title {
    font-size: 2rem;
  }
  
  .hero-subtitle {
    font-size: 1rem;
  }
  
  .tab-content {
    padding: 1.5rem 1rem;
  }
  
  .result-header {
    flex-direction: column;
    gap: 1rem;
    align-items: flex-start;
  }
  
  .url-display {
    flex-direction: column;
    align-items: flex-start;
    gap: 0.5rem;
  }
}
</style>
