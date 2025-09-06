import React, {useState, useEffect} from "react";
import axios from "axios";
import "./App.css";

function App(){
  const [file, setFile] = useState(null);
  const [status, setStatus] = useState(null);
  const [query, setQuery] = useState("");
  const [docId, setDocId] = useState("");
  const [answer, setAnswer] = useState(null);
  const [activeTab, setActiveTab] = useState("chat");
  const [history, setHistory] = useState([]);
  const [uploadProgress, setUploadProgress] = useState(0);
  const [isProcessing, setIsProcessing] = useState(false);
  const [showPreview, setShowPreview] = useState(false);
  const [pdfUrl, setPdfUrl] = useState(null);
  const [numPages, setNumPages] = useState(null);
  const [isFullScreen, setIsFullScreen] = useState(false);

  // Load history from localStorage on component mount
  useEffect(() => {
    const savedHistory = localStorage.getItem('chatHistory');
    if (savedHistory) {
      setHistory(JSON.parse(savedHistory));
    }
  }, []);

  // Save history to localStorage whenever it changes
  useEffect(() => {
    localStorage.setItem('chatHistory', JSON.stringify(history));
  }, [history]);

  const upload = async () => {
    if(!file) return alert("choose file");
    const f = new FormData();
    f.append("file", file);
    setStatus("Uploading...");
    setUploadProgress(0);
    setIsProcessing(true);
    
    try {
      const res = await axios.post(`${process.env.REACT_APP_API_URL || 'https://document-assistant-api.onrender.com'}/documents`, f, { 
        headers: { "Content-Type": "multipart/form-data" },
        timeout: 120000, // 2 minutes timeout
        onUploadProgress: (progressEvent) => {
          const percentCompleted = Math.round((progressEvent.loaded * 100) / progressEvent.total);
          setUploadProgress(percentCompleted);
        }
      });
      
      setStatus("Processing document...");
      setUploadProgress(100);
      
      // Simulate processing time with progress updates
      let progress = 0;
      const progressInterval = setInterval(() => {
        progress += 10;
        setUploadProgress(progress);
        if (progress >= 100) {
          clearInterval(progressInterval);
          setStatus("Uploaded. DocumentId: " + res.data.documentId);
          setDocId(res.data.documentId);
          setIsProcessing(false);
        }
      }, 500);
      
    } catch(e){
      setStatus("Error: " + (e.response?.data?.error || e.message));
      setIsProcessing(false);
      setUploadProgress(0);
    }
  };

  const ask = async () => {
    if(!query) return alert("enter question");
    setAnswer("Thinking...");
    const currentQuery = query;
    const currentDocId = docId;
    const timestamp = new Date().toISOString();
    
    try {
      const res = await axios.post(`${process.env.REACT_APP_PROCESSOR_URL || 'http://localhost:8000'}/qa`, { 
        query: currentQuery, 
        documentId: currentDocId || null, 
        top_k: 5 
      }, {
        timeout: 30000 // 30 seconds timeout for AI responses
      });
      
      const fullAnswer = res.data.answer;
      setAnswer(fullAnswer);
      
      // Add to history
      const newHistoryItem = {
        id: Date.now(),
        timestamp,
        query: currentQuery,
        docId: currentDocId,
        answer: fullAnswer,
        citations: res.data.citations
      };
      setHistory(prev => [newHistoryItem, ...prev]);
      
    } catch(e){
      const errorMessage = "Error: " + (e.response?.data?.detail || e.message);
      setAnswer(errorMessage);
      
      // Add error to history too
      const newHistoryItem = {
        id: Date.now(),
        timestamp,
        query: currentQuery,
        docId: currentDocId,
        answer: errorMessage,
        citations: []
      };
      setHistory(prev => [newHistoryItem, ...prev]);
    }
  };

  // History management functions
  const clearAllHistory = () => {
    if (window.confirm("Are you sure you want to clear all chat history?")) {
      setHistory([]);
    }
  };

  const deleteHistoryItem = (id) => {
    if (window.confirm("Are you sure you want to delete this conversation?")) {
      setHistory(prev => prev.filter(item => item.id !== id));
    }
  };

  const formatTimestamp = (timestamp) => {
    const date = new Date(timestamp);
    return date.toLocaleString();
  };

  const loadHistoryItem = (item) => {
    setQuery(item.query);
    setDocId(item.docId || "");
    setAnswer(item.answer);
    setActiveTab("chat");
  };

  // PDF Preview functions
  const togglePreview = () => {
    if (file && !showPreview) {
      // Create object URL for the selected file
      const url = URL.createObjectURL(file);
      setPdfUrl(url);
    }
    setShowPreview(!showPreview);
  };

  const closePreview = () => {
    setShowPreview(false);
    setIsFullScreen(false);
    if (pdfUrl) {
      URL.revokeObjectURL(pdfUrl);
      setPdfUrl(null);
    }
  };

  const toggleFullScreen = () => {
    setIsFullScreen(!isFullScreen);
  };

  // Handle keyboard shortcuts
  useEffect(() => {
    const handleKeyDown = (event) => {
      if (showPreview) {
        if (event.key === 'F11') {
          event.preventDefault();
          toggleFullScreen();
        } else if (event.key === 'Escape') {
          if (isFullScreen) {
            setIsFullScreen(false);
          } else {
            closePreview();
          }
        }
      }
    };

    document.addEventListener('keydown', handleKeyDown);
    return () => {
      document.removeEventListener('keydown', handleKeyDown);
    };
  }, [showPreview, isFullScreen]);

  return (
    <div className="app">
      {/* Header */}
      <header className="app-header">
        <div className="header-content">
          <div className="header-brand">
            <div className="brand-icon">🤖</div>
            <div className="brand-text">
              <h1 className="brand-title">AI Document Assistant</h1>
              <p className="brand-subtitle">Intelligent PDF Processing & Q&A</p>
            </div>
          </div>
          <nav className="header-nav">
            <a href="#features" className="nav-link">Features</a>
            <a href="#about" className="nav-link">About</a>
            <a href="#contact" className="nav-link">Contact</a>
          </nav>
        </div>
      </header>

      <div className="container">
        
        {/* Tab Navigation */}
        <div className="tab-navigation">
          <button 
            className={`tab-btn ${activeTab === 'chat' ? 'active' : ''}`}
            onClick={() => setActiveTab('chat')}
          >
            💬 Chat
          </button>
          <button 
            className={`tab-btn ${activeTab === 'history' ? 'active' : ''}`}
            onClick={() => setActiveTab('history')}
          >
            📚 History ({history.length})
          </button>
        </div>

        {/* Chat Tab */}
        {activeTab === 'chat' && (
          <div className="tab-content">
            <div className="upload-section">
              <h2 className="section-title">📄 Upload Document</h2>
              <div className="form-group">
                <input 
                  type="file" 
                  onChange={e=>setFile(e.target.files[0])} 
                  accept=".pdf"
                  className="file-input"
                />
                {file && (
                  <div className="file-selected">
                    📄 Selected: {file.name}
                  </div>
                )}
              </div>
              <div className="button-group">
                <button className="btn btn-primary" onClick={upload} disabled={isProcessing}>
                  {isProcessing ? "⏳ Processing..." : "📤 Upload PDF"}
                </button>
                <button className="btn btn-secondary" onClick={togglePreview} disabled={!file}>
                  👁️ Preview PDF
                </button>
              </div>
              
              {isProcessing && (
                <div className="progress-container">
                  <div className="progress-bar">
                    <div 
                      className="progress-fill" 
                      style={{ width: `${uploadProgress}%` }}
                    ></div>
                  </div>
                  <div className="progress-text">{uploadProgress}% Complete</div>
                </div>
              )}
              
              {status && (
                <div className={`status ${status.includes('Error') ? 'error' : status.includes('Uploaded') ? 'success' : 'info'}`}>
                  {status}
                </div>
              )}
            </div>

            <hr className="divider"/>
            
            <div className="qa-section">
              <h2 className="section-title">❓ Ask Questions</h2>
              <div className="form-group">
                <label>Document ID (optional):</label>
                <input 
                  placeholder="Leave empty to search all documents" 
                  value={docId} 
                  onChange={e=>setDocId(e.target.value)} 
                />
              </div>
              <div className="form-group">
                <label>Your Question:</label>
                <textarea 
                  placeholder="Ask anything about your uploaded documents..." 
                  rows={4} 
                  value={query} 
                  onChange={e=>setQuery(e.target.value)} 
                />
              </div>
              <button className="btn btn-secondary" onClick={ask}>
                🤖 Ask AI
              </button>
              {answer && (
                <div className={`answer ${answer.includes('Error') ? 'error' : answer.includes('Thinking') ? 'loading' : 'success'}`}>
                  {answer}
                </div>
              )}
            </div>
          </div>
        )}

        {/* History Tab */}
        {activeTab === 'history' && (
          <div className="tab-content">
            <div className="history-header">
              <h2 className="section-title">📚 Chat History</h2>
              {history.length > 0 && (
                <button className="btn btn-danger" onClick={clearAllHistory}>
                  🗑️ Clear All History
                </button>
              )}
            </div>
            
            {history.length === 0 ? (
              <div className="empty-state">
                <div className="empty-icon">📭</div>
                <h3>No conversations yet</h3>
                <p>Start chatting to see your conversation history here!</p>
              </div>
            ) : (
              <div className="history-list">
                {history.map((item) => (
                  <div key={item.id} className="history-item">
                    <div className="history-item-header">
                      <div className="history-meta">
                        <span className="history-time">{formatTimestamp(item.timestamp)}</span>
                        {item.docId && (
                          <span className="history-doc-id">Doc ID: {item.docId}</span>
                        )}
                      </div>
                      <div className="history-actions">
                        <button 
                          className="btn btn-small btn-primary"
                          onClick={() => loadHistoryItem(item)}
                        >
                          🔄 Load
                        </button>
                        <button 
                          className="btn btn-small btn-danger"
                          onClick={() => deleteHistoryItem(item.id)}
                        >
                          🗑️ Delete
                        </button>
                      </div>
                    </div>
                    <div className="history-query">
                      <strong>Q:</strong> {item.query}
                    </div>
                    <div className="history-answer">
                      <strong>A:</strong> {item.answer.substring(0, 200)}
                      {item.answer.length > 200 && "..."}
                    </div>
                  </div>
                ))}
              </div>
            )}
          </div>
        )}
      </div>

      {/* PDF Preview Modal */}
      {showPreview && (
        <div className={`pdf-preview-modal ${isFullScreen ? 'fullscreen' : ''}`}>
          <div className={`pdf-preview-content ${isFullScreen ? 'fullscreen' : ''}`}>
            {!isFullScreen && (
              <div className="pdf-preview-header">
                <h3>📄 PDF Preview</h3>
                <div className="pdf-preview-controls">
                  <button className="btn btn-secondary btn-small" onClick={toggleFullScreen}>
                    🔍 Full Screen
                  </button>
                  <button className="btn btn-danger btn-small" onClick={closePreview}>
                    ✕ Close
                  </button>
                </div>
              </div>
            )}
            {isFullScreen && (
              <div className="pdf-fullscreen-header">
                <div className="pdf-fullscreen-controls">
                  <button className="btn btn-secondary btn-small" onClick={toggleFullScreen}>
                    📱 Exit Full Screen
                  </button>
                  <button className="btn btn-danger btn-small" onClick={closePreview}>
                    ✕ Close
                  </button>
                </div>
                <div className="pdf-fullscreen-info">
                  <span>Press F11 or ESC to exit full screen</span>
                </div>
              </div>
            )}
            <div className={`pdf-viewer ${isFullScreen ? 'fullscreen' : ''}`}>
              <iframe
                src={pdfUrl}
                width="100%"
                height={isFullScreen ? "100%" : "600px"}
                title="PDF Preview"
                className={`pdf-iframe ${isFullScreen ? 'fullscreen' : ''}`}
              />
            </div>
          </div>
        </div>
      )}

      {/* Footer */}
      <footer className="app-footer">
        <div className="footer-content">
          <div className="footer-section">
            <h3 className="footer-title">AI Document Assistant</h3>
            <p className="footer-description">
              Advanced PDF processing with AI-powered question answering and full-screen document viewing.
            </p>
          </div>
          <div className="footer-section">
            <h4 className="footer-subtitle">Features</h4>
            <ul className="footer-links">
              <li><a href="#upload">PDF Upload & Processing</a></li>
              <li><a href="#qa">AI Question Answering</a></li>
              <li><a href="#preview">Full-Screen PDF Preview</a></li>
              <li><a href="#history">Chat History</a></li>
            </ul>
          </div>
          <div className="footer-section">
            <h4 className="footer-subtitle">Technology</h4>
            <ul className="footer-links">
              <li>React Frontend</li>
              <li>.NET Core API</li>
              <li>Python FastAPI</li>
              <li>PostgreSQL + pgvector</li>
            </ul>
          </div>
          <div className="footer-section">
            <h4 className="footer-subtitle">Quick Actions</h4>
            <div className="footer-actions">
              <button className="footer-btn" onClick={() => setActiveTab('chat')}>
                💬 Start Chatting
              </button>
              <button className="footer-btn" onClick={() => setActiveTab('history')}>
                📚 View History
              </button>
            </div>
          </div>
        </div>
        <div className="footer-bottom">
          <p>&copy; 2025 AI Document Assistant. Built with ❤️ for intelligent document processing.</p>
        </div>
      </footer>
    </div>
  );
}

export default App;
