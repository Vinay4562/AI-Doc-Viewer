#!/usr/bin/env python3
"""
Test script to verify all imports work correctly in the Docker environment.
This script can be run inside the Docker container to verify dependencies.
"""

def test_imports():
    """Test all required imports and report status."""
    print("Testing Python imports...")
    
    # Test basic imports
    try:
        import os, tempfile, pathlib, io, json
        print("✅ Basic Python modules: OK")
    except ImportError as e:
        print(f"❌ Basic Python modules: FAILED - {e}")
        return False
    
    # Test FastAPI imports
    try:
        from fastapi import FastAPI, HTTPException
        from fastapi.middleware.cors import CORSMiddleware
        from pydantic import BaseModel
        print("✅ FastAPI modules: OK")
    except ImportError as e:
        print(f"❌ FastAPI modules: FAILED - {e}")
        return False
    
    # Test database imports
    try:
        import psycopg2
        from psycopg2.extras import execute_values
        print("✅ Database modules: OK")
    except ImportError as e:
        print(f"❌ Database modules: FAILED - {e}")
        return False
    
    # Test scientific computing imports
    try:
        import numpy as np
        print("✅ NumPy: OK")
    except ImportError as e:
        print(f"❌ NumPy: FAILED - {e}")
        return False
    
    # Test PDF processing imports
    try:
        import fitz  # pymupdf
        print("✅ PyMuPDF: OK")
    except ImportError as e:
        print(f"❌ PyMuPDF: FAILED - {e}")
        return False
    
    # Test image processing imports
    try:
        from PIL import Image
        import pytesseract
        print("✅ Image processing modules: OK")
    except ImportError as e:
        print(f"❌ Image processing modules: FAILED - {e}")
        return False
    
    # Test AI imports
    try:
        import google.generativeai as genai
        print("✅ Google Generative AI: OK")
    except ImportError as e:
        print(f"❌ Google Generative AI: FAILED - {e}")
        return False
    
    # Test AWS/MinIO imports
    try:
        import boto3
        from botocore.client import Config
        print("✅ AWS/MinIO modules: OK")
    except ImportError as e:
        print(f"❌ AWS/MinIO modules: FAILED - {e}")
        return False
    
    print("\n🎉 All imports successful! The application should work correctly.")
    return True

if __name__ == "__main__":
    success = test_imports()
    exit(0 if success else 1)
