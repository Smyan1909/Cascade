"""
PaddleOCR gRPC Service

This package provides a gRPC service for PaddleOCR with Vision Transformer models.
It serves as a fallback OCR engine for the Cascade.Vision module when
Windows OCR and Tesseract fail to recognize text.

Usage:
    python -m paddle_ocr_service.server --port 50052
"""

__version__ = "0.1.0"

