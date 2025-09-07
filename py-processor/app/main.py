from fastapi import FastAPI, HTTPException, Form, File, UploadFile
from fastapi.middleware.cors import CORSMiddleware
from pydantic import BaseModel
import os, tempfile, pathlib, io, json
from datetime import datetime
import boto3
from botocore.client import Config
from typing import List, Optional

# Database imports with error handling
# Note: These imports may show linter warnings locally but work fine in Docker
try:
    import psycopg2
    from psycopg2.extras import execute_values
except ImportError:
    print("Warning: psycopg2 not found. Database functionality will be limited.")
    psycopg2 = None
    execute_values = None

# Scientific computing imports
try:
    import numpy as np
except ImportError:
    print("Warning: numpy not found. Some features may not work.")
    np = None

# PDF processing imports
# Note: These imports may show linter warnings locally but work fine in Docker
try:
    import fitz  # pymupdf
except ImportError:
    try:
        import pymupdf as fitz
    except ImportError:
        print("Warning: pymupdf/fitz not found. PDF processing will not work.")
        fitz = None

# Image processing imports
try:
    from PIL import Image
    import pytesseract
except ImportError:
    print("Warning: PIL or pytesseract not found. OCR functionality will not work.")
    Image = None
    pytesseract = None

# AI imports
try:
    import google.generativeai as genai
except ImportError:
    print("Warning: google-generativeai not found. AI functionality will not work.")
    genai = None

# Configuration - environment variables (set in docker-compose)
MINIO_ENDPOINT = os.getenv("MINIO_ENDPOINT", "minio:9000")
MINIO_ACCESS_KEY = os.getenv("MINIO_ACCESS_KEY", "minioadmin")
MINIO_SECRET_KEY = os.getenv("MINIO_SECRET_KEY", "minioadmin")
DATABASE_URL = os.getenv("DATABASE_URL", "postgresql://appuser:changeme@postgres:5432/docassistant")
REDIS_URL = os.getenv("REDIS_URL", "redis://localhost:6379")  # Redis connection URL
VECTOR_DIM = 384  # for all-MiniLM-L6-v2

# Gemini AI Configuration
GEMINI_API_KEY = os.getenv("GEMINI_API_KEY")
if GEMINI_API_KEY and genai:
    try:
        genai.configure(api_key=GEMINI_API_KEY)
        gemini_model = genai.GenerativeModel('gemini-2.0-flash-exp')
    except Exception as e:
        print(f"Warning: Failed to configure Gemini AI: {e}")
        gemini_model = None
else:
    print("Warning: GEMINI_API_KEY not set or google-generativeai not available")
    gemini_model = None

# Initialize clients
# Use https for external MinIO service, http for local
endpoint_url = f"https://{MINIO_ENDPOINT}" if MINIO_ENDPOINT.startswith("document-assistant-storage") else f"http://{MINIO_ENDPOINT}"
s3 = boto3.client("s3",
                  endpoint_url=endpoint_url,
                  aws_access_key_id=MINIO_ACCESS_KEY,
                  aws_secret_access_key=MINIO_SECRET_KEY,
                  config=Config(signature_version="s3v4"),
                  region_name="us-east-1")
# Simple embedding function (placeholder - in production use a proper embedding service)
def simple_embed(texts):
    # Return random embeddings for now - replace with actual embedding service
    if np is None:
        raise HTTPException(status_code=500, detail="NumPy not available")
    return np.random.random((len(texts), 384)).astype(np.float32)

def generate_ai_response(query: str, context: str) -> str:
    """
    Generate AI response using Google Gemini Flash 2.5
    """
    if genai is None or gemini_model is None:
        return "AI service not available. Please check configuration."
    
    try:
        prompt = f"""
You are an AI assistant that helps users find information from documents. 
Based on the following context from uploaded documents, please answer the user's question.

User Question: {query}

Context from documents:
{context}

Instructions:
1. Provide a clear, helpful answer based on the context provided
2. If the context doesn't contain enough information to answer the question, say so
3. Be concise but informative
4. If you reference specific information, mention that it came from the uploaded documents
5. If the question is not related to the document content, politely redirect to ask about the document content

Answer:
"""
        response = gemini_model.generate_content(prompt)
        return response.text
    except Exception as e:
        return f"I apologize, but I encountered an error while processing your question: {str(e)}. Please try again."

app = FastAPI(title="py-processor")

# Add CORS middleware
app.add_middleware(
    CORSMiddleware,
    allow_origins=[
        "http://localhost:3000",
        "https://ai-doc-viewer-frontend.onrender.com",
        "https://ai-doc-viewer-api.onrender.com"
    ],
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)

@app.get("/health")
async def health_check():
    """Health check endpoint for monitoring"""
    return {"status": "healthy", "service": "py-processor"}

@app.get("/test")
async def test_endpoint():
    """Test endpoint to verify service functionality"""
    # Test Redis connection
    redis_available = False
    try:
        import redis
        r = redis.from_url(REDIS_URL)
        r.ping()
        redis_available = True
    except:
        pass
    
    return {
        "message": "Processor service is working",
        "timestamp": datetime.now().isoformat(),
        "ai_available": gemini_model is not None,
        "database_available": psycopg2 is not None,
        "redis_available": redis_available,
        "minio_available": True
    }

@app.get("/debug")
async def debug_info():
    """Debug endpoint to check service configuration and connectivity"""
    debug_info = {
        "service": "py-processor",
        "timestamp": datetime.now().isoformat(),
        "environment": {
            "DATABASE_URL": DATABASE_URL[:20] + "..." if DATABASE_URL else "Not set",
            "REDIS_URL": REDIS_URL[:20] + "..." if REDIS_URL else "Not set",
            "MINIO_ENDPOINT": MINIO_ENDPOINT,
            "MINIO_ACCESS_KEY": MINIO_ACCESS_KEY[:10] + "..." if MINIO_ACCESS_KEY else "Not set",
            "GEMINI_API_KEY": "Set" if GEMINI_API_KEY else "Not set"
        },
        "dependencies": {
            "psycopg2": psycopg2 is not None,
            "numpy": np is not None,
            "fitz": fitz is not None,
            "PIL": Image is not None,
            "pytesseract": pytesseract is not None,
            "google_generativeai": genai is not None
        }
    }
    
    # Test database connection
    try:
        if psycopg2:
            conn = psycopg2.connect(DATABASE_URL)
            cur = conn.cursor()
            cur.execute("SELECT 1")
            cur.close()
            conn.close()
            debug_info["database"] = {"status": "ok", "connected": True}
        else:
            debug_info["database"] = {"status": "error", "message": "psycopg2 not available"}
    except Exception as e:
        debug_info["database"] = {"status": "error", "message": str(e)}
    
    # Test Redis connection
    try:
        import redis
        r = redis.from_url(REDIS_URL)
        r.ping()
        debug_info["redis"] = {"status": "ok", "connected": True}
    except Exception as e:
        debug_info["redis"] = {"status": "error", "message": str(e)}
    
    return debug_info

@app.post("/init-db")
async def init_database():
    """Initialize database tables for the processor service"""
    try:
        conn = pg_connect()
        cur = conn.cursor()
        
        # Create documents table
        cur.execute("""
            CREATE TABLE IF NOT EXISTS documents (
                id SERIAL PRIMARY KEY,
                title TEXT,
                file_url TEXT,
                status TEXT,
                created_at TIMESTAMPTZ DEFAULT NOW()
            )
        """)
        
        # Create document_pages table
        cur.execute("""
            CREATE TABLE IF NOT EXISTS document_pages (
                id SERIAL PRIMARY KEY,
                document_id INTEGER REFERENCES documents(id) ON DELETE CASCADE,
                page_no INTEGER NOT NULL,
                text TEXT NOT NULL
            )
        """)
        
        # Create chunks table (without vector column - pgvector extension not available)
        cur.execute("""
            CREATE TABLE IF NOT EXISTS chunks (
                id SERIAL PRIMARY KEY,
                document_id INTEGER REFERENCES documents(id) ON DELETE CASCADE,
                page_no INTEGER NOT NULL,
                text TEXT NOT NULL,
                created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
            )
        """)
        
        # Create index for text search
        cur.execute("""
            CREATE INDEX IF NOT EXISTS idx_chunks_text ON chunks USING gin(to_tsvector('english', text))
        """)
        
        # Create index for document_pages text search
        cur.execute("""
            CREATE INDEX IF NOT EXISTS idx_document_pages_text ON document_pages USING gin(to_tsvector('english', text))
        """)
        
        conn.commit()
        cur.close()
        conn.close()
        
        return {"message": "Database initialized successfully", "tables": ["documents", "document_pages", "chunks"]}
    except Exception as e:
        return {"error": f"Database initialization failed: {str(e)}"}

class ExtractIn(BaseModel):
    documentId: int
    fileUrl: str  # expected minio://bucket/key or http(s)

class QaIn(BaseModel):
    query: str
    documentId: Optional[int] = None
    top_k: int = 6

def pg_connect():
    if psycopg2 is None:
        raise HTTPException(status_code=500, detail="Database connection not available")
    return psycopg2.connect(DATABASE_URL)

def download_from_minio(url: str) -> str:
    """
    Downloads file from 'minio://bucket/key', 'file://path', or http(s) and returns local path.
    """
    if url.startswith("file://"):
        # Handle local file path
        file_path = url[7:]  # Remove 'file://' prefix
        if os.path.exists(file_path):
            return file_path
        else:
            raise ValueError(f"File not found: {file_path}")
    elif url.startswith("minio://"):
        _, _, path = url.partition("minio://")
        parts = path.split("/", 1)
        if len(parts) != 2:
            raise ValueError("minio url must be minio://bucket/key")
        bucket, key = parts
        local = tempfile.mktemp(suffix=pathlib.Path(key).suffix)
        os.makedirs(os.path.dirname(local), exist_ok=True)
        s3.download_file(bucket, key, local)
        return local
    else:
        # fallback: try HTTP download
        import requests
        resp = requests.get(url)
        if resp.status_code != 200:
            raise ValueError("failed to download")
        local = tempfile.mktemp(suffix=".bin")
        with open(local, "wb") as f:
            f.write(resp.content)
        return local

def extract_text_from_pdf(path: str) -> List[dict]:
    """
    Optimized PDF text extraction with smarter OCR usage.
    Only use OCR when text extraction yields very little content.
    """
    if fitz is None:
        raise HTTPException(status_code=500, detail="PDF processing not available")
    
    doc = fitz.open(path)
    pages = []
    
    for i, page in enumerate(doc):
        # Try text extraction first
        text = page.get_text("text") or ""
        
        # Only use OCR if text is very sparse (less than 20 characters)
        # and the page seems to have content (not blank)
        if len(text.strip()) < 20 and Image is not None and pytesseract is not None:
            try:
                # Check if page has any content by looking at images/rects
                if page.get_images() or page.get_drawings():
                    # Render as image and run OCR with optimized settings
                    pix = page.get_pixmap(dpi=150)  # Reduced DPI for faster processing
                    img = Image.frombytes("RGB", [pix.width, pix.height], pix.samples)
                    ocr = pytesseract.image_to_string(img, config='--psm 6')  # Optimized OCR config
                    text = (text + "\n" + ocr).strip()
            except Exception as e:
                # If OCR fails, keep the original text
                print(f"OCR failed for page {i+1}: {e}")
        
        pages.append({"page_no": i+1, "text": text})
    
    doc.close()
    return pages

def chunk_text(text: str, chunk_size=500, overlap=50):
    """
    Optimized text chunking with better overlap strategy.
    """
    if not text or len(text.strip()) == 0:
        return []
    
    # Split by sentences first for better context preservation
    sentences = text.split('. ')
    chunks = []
    current_chunk = ""
    
    for sentence in sentences:
        # If adding this sentence would exceed chunk size, save current chunk
        if len(current_chunk) + len(sentence) > chunk_size and current_chunk:
            chunks.append(current_chunk.strip())
            # Start new chunk with overlap from previous chunk
            overlap_text = current_chunk[-overlap:] if len(current_chunk) > overlap else current_chunk
            current_chunk = overlap_text + ". " + sentence
        else:
            current_chunk += ". " + sentence if current_chunk else sentence
    
    # Add the last chunk if it has content
    if current_chunk.strip():
        chunks.append(current_chunk.strip())
    
    return chunks

@app.post("/process/extract")
async def extract(inp: ExtractIn):
    try:
        local = download_from_minio(inp.fileUrl)
    except Exception as e:
        raise HTTPException(status_code=400, detail=f"download failed: {e}")

    pages = []
    try:
        pages = extract_text_from_pdf(local)
    except Exception as e:
        # If not a PDF, try simple OCR of image
        try:
            if Image is not None and pytesseract is not None:
                img = Image.open(local)
                text = pytesseract.image_to_string(img)
                pages = [{"page_no": 1, "text": text}]
            else:
                raise HTTPException(status_code=500, detail="OCR not available")
        except Exception as e2:
            raise HTTPException(status_code=500, detail=f"extraction failed: {e}; {e2}")

    # First, insert document record
    conn = pg_connect()
    cur = conn.cursor()
    
    try:
        # Insert document record
        cur.execute("""
            INSERT INTO documents (id, title, file_url, status, created_at) 
            VALUES (%s, %s, %s, %s, NOW())
            ON CONFLICT (id) DO UPDATE SET 
                title = EXCLUDED.title,
                file_url = EXCLUDED.file_url,
                status = EXCLUDED.status
        """, (inp.documentId, f"Document {inp.documentId}", inp.fileUrl, "processed"))
        
        # Insert pages into document_pages
        if execute_values is None:
            raise HTTPException(status_code=500, detail="Database utilities not available")
        
        if pages:
            execute_values(cur,
                           "INSERT INTO document_pages(document_id, page_no, text) VALUES %s",
                           [(inp.documentId, p["page_no"], p["text"]) for p in pages])
        
        conn.commit()
        
        # Enqueue embedding task (for simplicity run inline here)
        await embed_document(inp.documentId)
        
        return {"ok": True, "pages": len(pages), "documentId": inp.documentId}
        
    except Exception as e:
        conn.rollback()
        raise HTTPException(status_code=500, detail=f"Database operation failed: {str(e)}")
    finally:
        cur.close()
        conn.close()

@app.post("/process/extract-form")
async def extract_form(
    documentId: int = Form(...),
    fileUrl: str = Form(...)
):
    """Alternative endpoint that accepts form data directly"""
    inp = ExtractIn(documentId=documentId, fileUrl=fileUrl)
    return await extract(inp)

async def embed_document(document_id: int):
    """
    Optimized document embedding with better chunking and batch processing.
    """
    # fetch pages
    conn = pg_connect()
    cur = conn.cursor()
    cur.execute("SELECT id, page_no, text FROM document_pages WHERE document_id=%s", (document_id,))
    rows = cur.fetchall()
    cur.close()
    conn.close()
    
    all_chunks = []
    for rid, page_no, text in rows:
        if not text or len(text.strip()) == 0: 
            continue
        
        # Use optimized chunking with smaller chunks for better search
        chunks = chunk_text(text, chunk_size=300, overlap=30)
        for idx, c in enumerate(chunks):
            all_chunks.append((document_id, page_no, idx, c))
    
    if not all_chunks:
        return True
    
    # Store chunks in batches for better performance
    conn = pg_connect()
    cur = conn.cursor()
    
    # Process in batches of 100 chunks
    batch_size = 100
    for i in range(0, len(all_chunks), batch_size):
        batch = all_chunks[i:i + batch_size]
        records = [(r[0], r[1], r[3]) for r in batch]  # Fixed: use r[3] for text, not r[2]
        
        if execute_values is None:
            raise HTTPException(status_code=500, detail="Database utilities not available")
        execute_values(cur,
                       "INSERT INTO chunks(document_id, page_no, text) VALUES %s",
                       records)
    
    conn.commit()
    cur.close()
    conn.close()
    return True

@app.post("/qa")
async def qa(inp: QaIn):
    """
    Fixed Q&A with corrected search algorithm and better error handling.
    """
    conn = None
    cur = None
    
    try:
        # Validate input
        if not inp.query or not inp.query.strip():
            raise HTTPException(status_code=422, detail="Query cannot be empty")
        
        conn = pg_connect()
        cur = conn.cursor()
        
        # Simplified search algorithm - use any word longer than 1 character
        query_words = [word.lower().strip() for word in inp.query.split() if len(word.strip()) > 1]
        
        if not query_words:
            return {
                "answer": "Please provide a more specific question with meaningful keywords.",
                "citations": []
            }
        
        # Build search query - use OR for broader matching
        search_conditions = []
        params = []
        
        for word in query_words:
            search_conditions.append("text ILIKE %s")
            params.append(f"%{word}%")
        
        where_clause = " OR ".join(search_conditions)
        
        # Check if chunks table exists and has data
        cur.execute("SELECT COUNT(*) FROM chunks")
        chunk_count = cur.fetchone()[0]
        
        if chunk_count == 0:
            return {
                "answer": "No documents have been processed yet. Please upload and process a document first.",
                "citations": []
            }
        
        # Build the search query
        if inp.documentId:
            # Verify document exists
            cur.execute("SELECT id FROM documents WHERE id = %s", (inp.documentId,))
            if not cur.fetchone():
                return {
                    "answer": f"Document with ID {inp.documentId} not found. Please check the document ID or leave it empty to search all documents.",
                    "citations": []
                }
            
            sql = f"SELECT id, document_id, page_no, text FROM chunks WHERE document_id=%s AND ({where_clause}) LIMIT %s"
            params = [inp.documentId] + params + [inp.top_k]
        else:
            sql = f"SELECT id, document_id, page_no, text FROM chunks WHERE {where_clause} LIMIT %s"
            params = params + [inp.top_k]
        
        cur.execute(sql, params)
        rows = cur.fetchall()
        
        # Build context with better formatting
        if rows:
            context_parts = []
            for r in rows:
                context_parts.append(f"[Document {r[1]}, Page {r[2]}]: {r[3]}")
            context = "\n\n".join(context_parts)
            
            # Generate AI response with optimized prompt
            answer = generate_ai_response(inp.query, context)
            citations = [{"documentId": r[1], "page": r[2], "score": 1.0} for r in rows]
        else:
            answer = "I couldn't find any relevant information in the uploaded documents to answer your question. Please try rephrasing your question or make sure you have uploaded relevant documents."
            citations = []
        
        return {"answer": answer, "citations": citations}
        
    except HTTPException:
        # Re-raise HTTP exceptions as-is
        raise
    except Exception as e:
        raise HTTPException(status_code=500, detail=f"Q&A processing failed: {str(e)}")
    finally:
        if cur:
            cur.close()
        if conn:
            conn.close()
