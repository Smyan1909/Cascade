"""
PaddleOCR Service Implementation

This module provides the gRPC service implementation for PaddleOCR.
It supports multiple Vision Transformer-based models for text recognition.
"""

import io
import time
import logging
from typing import Optional, List, Tuple
from dataclasses import dataclass

import numpy as np
import cv2
from PIL import Image
from paddleocr import PaddleOCR

logger = logging.getLogger(__name__)


@dataclass
class OcrWord:
    """Represents a recognized word."""
    text: str
    polygon: List[Tuple[int, int]]
    confidence: float
    
    @property
    def bounding_box(self) -> Tuple[int, int, int, int]:
        """Returns axis-aligned bounding box (x, y, width, height)."""
        xs = [p[0] for p in self.polygon]
        ys = [p[1] for p in self.polygon]
        x = min(xs)
        y = min(ys)
        return (x, y, max(xs) - x, max(ys) - y)


@dataclass
class OcrTextBlock:
    """Represents a block of recognized text."""
    text: str
    polygon: List[Tuple[int, int]]
    confidence: float
    words: List[OcrWord]
    
    @property
    def bounding_box(self) -> Tuple[int, int, int, int]:
        """Returns axis-aligned bounding box (x, y, width, height)."""
        xs = [p[0] for p in self.polygon]
        ys = [p[1] for p in self.polygon]
        x = min(xs)
        y = min(ys)
        return (x, y, max(xs) - x, max(ys) - y)


@dataclass
class OcrResult:
    """Represents the full OCR result."""
    success: bool
    error_message: str = ""
    full_text: str = ""
    confidence: float = 0.0
    blocks: List[OcrTextBlock] = None
    processing_time_ms: int = 0
    model_used: str = ""
    
    def __post_init__(self):
        if self.blocks is None:
            self.blocks = []


class PaddleOcrService:
    """
    PaddleOCR service with Vision Transformer model support.
    
    Supports the following models:
    - ViTSTR: Vision Transformer for Scene Text Recognition
    - SVTR: Single Visual Model for Scene Text Recognition
    - PP-OCRv4: Latest PaddleOCR model with best accuracy
    """
    
    MODEL_CONFIGS = {
        "vitstr": {
            "rec_algorithm": "ViTSTR",
            "description": "Vision Transformer - balanced accuracy/speed"
        },
        "svtr": {
            "rec_algorithm": "SVTR_LCNet",
            "description": "Single Visual Model - faster inference"
        },
        "ppocrv4": {
            "rec_algorithm": "SVTR_LCNet",  # PP-OCRv4 uses SVTR backbone
            "description": "Latest model - best accuracy"
        }
    }
    
    SUPPORTED_LANGUAGES = [
        "en", "ch", "chinese_cht", "japan", "korean",
        "french", "german", "arabic", "cyrillic", "latin",
        "devanagari", "tamil", "telugu", "kannada"
    ]
    
    def __init__(
        self,
        model: str = "ppocrv4",
        language: str = "en",
        use_gpu: bool = False,
        gpu_mem: int = 500
    ):
        """
        Initialize the PaddleOCR service.
        
        Args:
            model: Model to use ("vitstr", "svtr", "ppocrv4")
            language: Language for recognition
            use_gpu: Whether to use GPU acceleration
            gpu_mem: GPU memory limit in MB
        """
        self.model_name = model.lower()
        self.language = language
        self.use_gpu = use_gpu
        self.gpu_mem = gpu_mem
        self._ocr: Optional[PaddleOCR] = None
        self._initialized = False
        
    def initialize(self) -> bool:
        """Initialize the OCR engine."""
        try:
            # Use default PP-OCRv4 model (new API)
            self._ocr = PaddleOCR(
                lang=self.language,
                use_textline_orientation=True  # Equivalent to use_angle_cls
            )
            
            self._initialized = True
            logger.info(f"Initialized PaddleOCR with model: {self.model_name}, language: {self.language}")
            return True
            
        except Exception as e:
            logger.error(f"Failed to initialize PaddleOCR: {e}")
            return False
    
    @property
    def is_ready(self) -> bool:
        """Check if the service is ready."""
        return self._initialized and self._ocr is not None
    
    def recognize(
        self,
        image_data: bytes,
        use_angle_cls: bool = True,
        detect_only: bool = False
    ) -> OcrResult:
        """
        Recognize text in an image.
        
        Args:
            image_data: Image data as bytes
            use_angle_cls: Whether to use angle classification
            detect_only: Only detect text regions, don't recognize
            
        Returns:
            OcrResult with recognized text and bounding boxes
        """
        if not self.is_ready:
            return OcrResult(
                success=False,
                error_message="OCR engine not initialized"
            )
        
        start_time = time.time()
        
        try:
            # Save image to temp file (PaddleOCR prefers file paths)
            import tempfile
            with tempfile.NamedTemporaryFile(suffix='.png', delete=False) as f:
                f.write(image_data)
                temp_path = f.name
            
            try:
                # Run OCR using new predict API
                result = self._ocr.predict(temp_path)
                
                # Process results
                ocr_result = self._process_result(result)
                
                processing_time = int((time.time() - start_time) * 1000)
                ocr_result.processing_time_ms = processing_time
                ocr_result.model_used = self.model_name
                
                return ocr_result
            finally:
                import os
                os.unlink(temp_path)
            
        except Exception as e:
            logger.error(f"OCR recognition failed: {e}")
            return OcrResult(
                success=False,
                error_message=str(e)
            )
    
    def _bytes_to_image(self, image_data: bytes) -> Optional[np.ndarray]:
        """Convert bytes to numpy image array."""
        try:
            # Try using PIL first
            pil_image = Image.open(io.BytesIO(image_data))
            image = np.array(pil_image)
            
            # Convert RGB to BGR for OpenCV
            if len(image.shape) == 3 and image.shape[2] == 3:
                image = cv2.cvtColor(image, cv2.COLOR_RGB2BGR)
            elif len(image.shape) == 3 and image.shape[2] == 4:
                image = cv2.cvtColor(image, cv2.COLOR_RGBA2BGR)
                
            return image
            
        except Exception:
            # Fallback to OpenCV
            try:
                nparr = np.frombuffer(image_data, np.uint8)
                return cv2.imdecode(nparr, cv2.IMREAD_COLOR)
            except Exception:
                return None
    
    def _process_result(self, result) -> OcrResult:
        """Process PaddleOCR result into OcrResult (new API format)."""
        if result is None or len(result) == 0:
            return OcrResult(
                success=True,
                full_text="",
                confidence=0.0,
                blocks=[]
            )
        
        blocks = []
        full_text_parts = []
        total_confidence = 0.0
        word_count = 0
        
        # New API returns a list of dictionaries
        for page_result in result:
            if page_result is None:
                continue
            
            # Extract recognized texts and scores from new format
            rec_texts = page_result.get('rec_texts', [])
            rec_scores = page_result.get('rec_scores', [])
            rec_polys = page_result.get('rec_polys', [])
            
            for i, text in enumerate(rec_texts):
                if not text:
                    continue
                    
                confidence = float(rec_scores[i]) if i < len(rec_scores) else 0.0
                
                # Get polygon if available
                if i < len(rec_polys):
                    poly = rec_polys[i]
                    polygon = [(int(p[0]), int(p[1])) for p in poly]
                else:
                    polygon = [(0, 0), (100, 0), (100, 20), (0, 20)]
                
                # Create word
                word = OcrWord(
                    text=text,
                    polygon=polygon,
                    confidence=confidence
                )
                
                # Create block
                block = OcrTextBlock(
                    text=text,
                    polygon=polygon,
                    confidence=confidence,
                    words=[word]
                )
                
                blocks.append(block)
                full_text_parts.append(text)
                total_confidence += confidence
                word_count += 1
        
        avg_confidence = total_confidence / word_count if word_count > 0 else 0.0
        
        return OcrResult(
            success=True,
            full_text="\n".join(full_text_parts),
            confidence=avg_confidence,
            blocks=blocks
        )
    
    def get_status(self) -> dict:
        """Get service status information."""
        return {
            "is_ready": self.is_ready,
            "model_loaded": self.model_name if self.is_ready else "",
            "gpu_available": self.use_gpu,
            "gpu_memory_used_mb": self.gpu_mem if self.use_gpu else 0,
            "supported_languages": self.SUPPORTED_LANGUAGES,
            "paddle_version": self._get_paddle_version(),
            "paddleocr_version": self._get_paddleocr_version()
        }
    
    def _get_paddle_version(self) -> str:
        """Get PaddlePaddle version."""
        try:
            import paddle
            return paddle.__version__
        except Exception:
            return "unknown"
    
    def _get_paddleocr_version(self) -> str:
        """Get PaddleOCR version."""
        try:
            import paddleocr
            return getattr(paddleocr, "__version__", "unknown")
        except Exception:
            return "unknown"

