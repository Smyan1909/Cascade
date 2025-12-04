from dataclasses import dataclass
from typing import Optional


@dataclass
class PaddleModelConfig:
    model_name: str = "PP-OCRv4"
    use_gpu: bool = False
    language: str = "en"


class ModelRegistry:
    """Simple placeholder registry until the full Paddle model loader is implemented."""

    def __init__(self) -> None:
        self._config = PaddleModelConfig()

    @property
    def config(self) -> PaddleModelConfig:
        return self._config

    def load(self) -> None:
        # Real implementation will initialize PaddleOCR instances here.
        pass


