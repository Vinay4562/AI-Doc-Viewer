from fastapi import FastAPI, HTTPException
from fastapi.middleware.cors import CORSMiddleware
from pydantic import BaseModel
import os, tempfile, pathlib, io, json
import boto3
from botocore.client import Config
from typing import List, Optional

# Database imports with error handling
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

# AI imports
try:
    import google.generativeai as genai
except ImportError:
    print("Warning: google-generativeai not found. AI functionality will not work.")
    genai = None

# Configuration - environment variables
MINIO_ENDPOINT = os.getenv("MINIO_ENDPOINT", "minio:9000")
MINIO_ACCESS_KEY = os.getenv("MINIO_ACCESS_KEY", "minioadmin")
MINIO_SECRET_KEY = os.getenv("MINIO_SECRET_KEY", "minioadmin")
DATABASE_URL = os.getenv("DATABASE_URL", "postgresql://appuser:changeme@postgres:5432/docassistant")
VECTOR_DIM = 384  # for all-MiniLM-L6-v2

# Gemini AI Configuration
GEMINI_API_KEY = os.getenv("GEMINI_API_KEY")
if not GEMINI_API_KEY:
    raise ValueError("GEMINI_API_KEY environment variable is required")
genai.configure(api_key=GEMINI_API_KEY)
gemini_model = genai.GenerativeModel('gemini-2.0-flash-exp')

# Initialize clients
s3 = boto3.client("s3",
                  endpoint_url=f"http://{MINIO_ENDPOINT}",
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
    if genai is None:
        return "AI service not available. Please check configuration."
    
    try:
        prompt = f"""
You are an AI assistant that helps users find information from documents. 
Based on the following context from uploaded documents, please answer the user's question.

User Question: {query}

Context from documents:
{context}

Please provide a helpful and accurate answer based on the context provided. If the context doesn't contain enough information to answer the question, please say so.
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

class QaIn(BaseModel):
    query: str
    documentId: Optional[int] = None
    top_k: int = 6

def pg_connect():
    if psycopg2 is None:
        raise HTTPException(status_code=500, detail="Database connection not available")
    return psycopg2.connect(DATABASE_URL)

@app.post("/qa")
async def qa(inp: QaIn):
    """
    Simplified Q&A endpoint for Render deployment
    """
    conn = pg_connect()
    cur = conn.cursor()
    
    # Simplified search algorithm - use any word longer than 1 character
    query_words = [word.lower().strip() for word in inp.query.split() if len(word.strip()) > 1]
    
    if not query_words:
        cur.close()
        conn.close()
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
    
    if inp.documentId:
        sql = f"SELECT id, document_id, page_no, text FROM chunks WHERE document_id=%s AND ({where_clause}) LIMIT %s"
        params = [inp.documentId] + params + [inp.top_k]
    else:
        sql = f"SELECT id, document_id, page_no, text FROM chunks WHERE {where_clause} LIMIT %s"
        params = params + [inp.top_k]
    
    cur.execute(sql, params)
    rows = cur.fetchall()
    cur.close()
    conn.close()
    
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

@app.get("/")
async def root():
    """Root endpoint"""
    return {"message": "AI Document Processor API", "status": "running"}
