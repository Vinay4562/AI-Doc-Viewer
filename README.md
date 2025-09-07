# 🤖 AI-Powered Document Assistant

A comprehensive document processing and question-answering system with advanced PDF viewing capabilities, intelligent text extraction, and AI-powered Q&A functionality.

## ✨ Key Features

### 📄 **Advanced PDF Processing**
- **Smart PDF Upload**: Drag-and-drop PDF file upload with progress tracking
- **Intelligent Text Extraction**: PyMuPDF-based extraction with OCR fallback using Tesseract
- **Full-Screen PDF Preview**: Immersive document viewing with keyboard shortcuts (F11, ESC)
- **Responsive Design**: Optimized for desktop and mobile viewing

### 🧠 **AI-Powered Question Answering**
- **Google Gemini Integration**: Advanced AI responses using Gemini 2.0 Flash
- **Context-Aware Answers**: Intelligent document context retrieval and processing
- **Citation Support**: Source references with document and page information
- **Multi-Document Search**: Query across all uploaded documents or specific ones

### 💬 **Interactive Chat Interface**
- **Real-time Chat**: Instant AI responses with typing indicators
- **Chat History**: Persistent conversation history with search and management
- **Smart Context**: Automatic document context integration
- **Export Capabilities**: Save and manage conversation history

### 🎨 **Modern User Interface**
- **Professional Header**: Branded navigation with smooth animations
- **Comprehensive Footer**: Feature overview and quick actions
- **Tabbed Interface**: Organized chat and history sections
- **Responsive Design**: Mobile-first approach with adaptive layouts

## 🏗️ **Technical Architecture**

### **Frontend (React)**
- Modern React 18 with hooks and functional components
- Responsive CSS with gradient themes and animations
- Full-screen PDF viewing with iframe optimization
- Local storage for chat history persistence
- Keyboard shortcuts and accessibility features

### **Backend Services**
- **ASP.NET Core 8 API**: Document upload and management gateway
- **Python FastAPI**: Advanced document processing and AI integration
- **PostgreSQL + pgvector**: Vector storage for semantic search
- **MinIO**: Scalable object storage for document files
- **Redis**: Caching and task queue management

### **AI & Processing**
- **Google Gemini 2.0 Flash**: Advanced language model for Q&A
- **PyMuPDF**: High-performance PDF text extraction
- **Tesseract OCR**: Fallback text extraction for scanned documents
- **Vector Embeddings**: Semantic search and document chunking
- **Smart Chunking**: Context-preserving text segmentation

## 🚀 **Quick Start**

### **Prerequisites**
1. **Docker & Docker Compose**: Ensure Docker is installed and running
2. **Tesseract OCR** (optional): For scanned document processing
   - Ubuntu: `sudo apt-get install -y tesseract-ocr`
   - Windows: Download from [GitHub releases](https://github.com/UB-Mannheim/tesseract/wiki)
3. **PostgreSQL with pgvector**: Vector extension for semantic search

### **Installation & Running**
```bash
# Clone the repository
git clone <repository-url>
cd document_assistant_repo_full

# Start all services
docker-compose up --build

# Access the application
# Frontend: http://localhost:3000
# .NET API: http://localhost:5000
# Python API: http://localhost:8000
# MinIO Console: http://localhost:9000
```

### **Usage Guide**

#### **1. Upload Documents**
- Click "📤 Upload PDF" to select and upload PDF files
- Monitor upload progress with real-time indicators
- Documents are automatically processed and indexed

#### **2. Preview Documents**
- Click "👁️ Preview PDF" to view uploaded documents
- Use "🔍 Full Screen" for immersive viewing
- **Keyboard Shortcuts**:
  - `F11`: Toggle full-screen mode
  - `ESC`: Exit full-screen or close preview

#### **3. Ask Questions**
- Enter questions in the chat interface
- Specify Document ID for targeted queries (optional)
- Get AI-powered answers with source citations
- View conversation history in the History tab

## 🔧 **Configuration**

### **Environment Variables**
```env
# Database
DATABASE_URL=

# MinIO Storage
MINIO_ENDPOINT=minio:9000
MINIO_ACCESS_KEY=
MINIO_SECRET_KEY=

# AI Services
GEMINI_API_KEY=
```

### **Production Deployment**
- Update secrets in `docker-compose.yml` or use a secrets manager
- Configure proper CORS settings for your domain
- Set up SSL certificates for HTTPS
- Consider using managed database services

## 📊 **Performance Features**

- **Optimized PDF Processing**: Smart chunking with overlap preservation
- **Vector Search**: Fast semantic search using pgvector
- **Caching**: Redis-based caching for improved response times
- **Responsive UI**: Mobile-optimized interface with touch support
- **Memory Management**: Efficient document processing with cleanup

## 🛠️ **Development**

### **Adding New Features**
- **Frontend**: React components in `frontend/src/`
- **Backend**: .NET API in `dotnet-api/`
- **Processing**: Python services in `py-processor/`
- **Database**: Migrations in `dotnet-api/migrations/`

### **Testing**
```bash
# Run frontend tests
cd frontend && npm test

# Run backend tests
cd dotnet-api && dotnet test

# Run Python tests
cd py-processor && python -m pytest
```

## 📈 **Future Enhancements**

- [ ] **Multi-format Support**: DOCX, TXT, and image file processing
- [ ] **Advanced Analytics**: Document insights and usage statistics
- [ ] **User Authentication**: OAuth and SSO integration
- [ ] **API Rate Limiting**: Production-ready request throttling
- [ ] **Kubernetes Deployment**: Helm charts for cloud deployment
- [ ] **Real-time Collaboration**: Multi-user document sharing
- [ ] **Advanced Search**: Filters, sorting, and faceted search

## 📄 **License**

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## 🤝 **Contributing**

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

---

**Built with ❤️ for intelligent document processing**
