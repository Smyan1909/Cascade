"""
Semantic tree + screenshot utilities for Python Playwright backend.

These functions produce Cascade's existing Python models so callers can return
the same shapes as the gRPC-backed tools.
"""

from __future__ import annotations

import base64
from dataclasses import dataclass
from io import BytesIO
from typing import Any, Dict, List, Optional, Tuple

from PIL import Image, ImageDraw, ImageFont
from playwright.sync_api import Page

from cascade_client.models import (
    ControlType,
    ImageFormat,
    Mark,
    NormalizedRectangle,
    PlatformSource,
    Screenshot,
    SemanticTree,
    UIElement,
)


DEFAULT_QUERY = "button, input, select, textarea, a, [role]"


@dataclass
class PwElementInfo:
    element_id: str
    name: str
    role: str
    tag: str
    bbox: Optional[Tuple[float, float, float, float]]  # x, y, w, h in px


def get_semantic_tree(page: Page, *, max_nodes: int = 256, query: str = DEFAULT_QUERY) -> SemanticTree:
    """
    Extract a shallow semantic tree from the DOM.

    This intentionally mirrors the existing C# Playwright provider behavior:
    it collects interactive controls and provides a flat list (parent_id empty).
    """
    # Ensure DOM is usable; do not block indefinitely.
    try:
        page.wait_for_load_state("domcontentloaded", timeout=10_000)
    except Exception:
        pass

    viewport = page.viewport_size or {"width": 1280, "height": 720}
    vw = float(viewport.get("width") or 1280)
    vh = float(viewport.get("height") or 720)
    try:
        dims = page.evaluate(
            "() => ({width: document.documentElement.scrollWidth || window.innerWidth || 1280, "
            "height: document.documentElement.scrollHeight || window.innerHeight || 720})"
        )
        doc_w = float(dims.get("width") or vw)
        doc_h = float(dims.get("height") or vh)
    except Exception:
        doc_w, doc_h = vw, vh

    # Use document dimensions for normalization so full_page screenshots can be marked consistently.
    norm_w = max(doc_w, vw)
    norm_h = max(doc_h, vh)

    # Collect node infos in JS for speed, then request bounding boxes via locator.
    # We still need bbox per node; playwright can't return DOMRect for detached nodes reliably.
    handles = page.query_selector_all(query)
    handles = handles[: max(0, int(max_nodes))]

    elements: List[UIElement] = []
    for idx, h in enumerate(handles):
        try:
            bbox = h.bounding_box()
        except Exception:
            bbox = None
        if not bbox or bbox.get("width", 0) <= 0 or bbox.get("height", 0) <= 0:
            continue

        try:
            dom_id = (h.get_attribute("id") or "").strip()
        except Exception:
            dom_id = ""

        # Name: prefer aria-label/name; fallback to innerText.
        try:
            name = h.evaluate(
                "el => (el.getAttribute('aria-label') || el.getAttribute('name') || el.innerText || '').trim()"
            )
        except Exception:
            name = ""

        try:
            role = h.get_attribute("role") or ""
        except Exception:
            role = ""

        try:
            tag = h.evaluate("el => (el.tagName || '').toLowerCase()")
        except Exception:
            tag = ""

        # Use a locator-like id when DOM id is missing, so the agent can act on it.
        css_selector = ""
        if not dom_id:
            try:
                css_selector = h.evaluate(
                    """el => {
  function cssPath(node) {
    if (!node || node.nodeType !== 1) return '';
    if (node.id) return '#' + node.id;
    const parts = [];
    while (node && node.nodeType === 1 && node !== document.body) {
      let selector = node.nodeName.toLowerCase();
      if (node.classList && node.classList.length) {
        // keep just one class to avoid overfitting
        selector += '.' + node.classList[0];
      }
      const parent = node.parentNode;
      if (parent) {
        const siblings = Array.from(parent.children).filter(n => n.nodeName === node.nodeName);
        if (siblings.length > 1) {
          const index = siblings.indexOf(node) + 1;
          selector += `:nth-of-type(${index})`;
        }
      }
      parts.unshift(selector);
      node = parent;
    }
    return parts.join(' > ');
  }
  return cssPath(el);
}"""
                )
                css_selector = (css_selector or "").strip()
            except Exception:
                css_selector = ""

        element_id = dom_id or (f"css={css_selector}" if css_selector else f"pw-{idx+1}")

        rect = NormalizedRectangle(
            x=_clamp01(float(bbox["x"]) / norm_w),
            y=_clamp01(float(bbox["y"]) / norm_h),
            width=_clamp01(float(bbox["width"]) / norm_w),
            height=_clamp01(float(bbox["height"]) / norm_h),
        )

        elem = UIElement(
            id=element_id,
            name=name or "",
            control_type=_map_control_type(tag),
            bounding_box=rect,
            parent_id="",
            platform_source=PlatformSource.WEB,
            aria_role=role or None,
            automation_id=dom_id or (css_selector or None),
            value_text=None,
        )
        elements.append(elem)

    return SemanticTree(elements=elements)


def get_screenshot(page: Page, *, full_page: bool = True) -> Screenshot:
    """
    Capture screenshot bytes.
    """
    bytes_png = page.screenshot(full_page=bool(full_page), type="png")
    return Screenshot(image=bytes_png, format=ImageFormat.PNG, marks=[])


def get_marked_screenshot(
    page: Page,
    tree: Optional[SemanticTree] = None,
    *,
    full_page: bool = True,
    font_size: int = 18,
    stroke_width: int = 2,
    max_marks: int = 256,
) -> Screenshot:
    """
    Return a screenshot with numeric marks drawn onto the image.
    Marks are aligned to element bounding boxes in `tree` (normalized coords).
    """
    base = get_screenshot(page, full_page=full_page)
    if not tree or not tree.elements:
        return base

    img = Image.open(BytesIO(base.image)).convert("RGBA")
    draw = ImageDraw.Draw(img)

    try:
        font = ImageFont.truetype("arial.ttf", font_size)
    except Exception:
        font = ImageFont.load_default()

    marks: List[Mark] = []
    for i, el in enumerate(tree.elements[: max_marks], start=1):
        if not el.bounding_box:
            continue
        label = str(i)
        marks.append(Mark(element_id=el.id, label=label))
        _draw_mark(
            draw,
            img.size,
            el.bounding_box,
            label,
            font=font,
            font_size=font_size,
            stroke_width=stroke_width,
        )

    out = BytesIO()
    img.convert("RGB").save(out, format="PNG")
    return Screenshot(image=out.getvalue(), format=ImageFormat.PNG, marks=marks)


def screenshot_to_tool_payload(screenshot: Screenshot) -> Dict[str, Any]:
    """
    Convert Screenshot model into the same dict shape returned by cascade_client.tools.get_screenshot().
    """
    return {
        "success": True,
        "image": base64.b64encode(screenshot.image).decode("utf-8"),
        "format": "PNG" if screenshot.format == ImageFormat.PNG else screenshot.format.name,
        "marks": [{"element_id": m.element_id, "label": m.label} for m in screenshot.marks],
    }


def semantic_tree_to_tool_payload(tree: SemanticTree) -> Dict[str, Any]:
    return {
        "success": True,
        "elements": [
            {
                "id": e.id,
                "name": e.name,
                "control_type": e.control_type.name,
                "platform_source": e.platform_source.name,
                "parent_id": e.parent_id,
            }
            for e in tree.elements
        ],
    }


def _map_control_type(tag: str) -> ControlType:
    t = (tag or "").strip().lower()
    if t == "button":
        return ControlType.BUTTON
    if t in ("input", "textarea"):
        return ControlType.INPUT
    if t == "select":
        return ControlType.COMBO
    if t == "a":
        return ControlType.BUTTON
    return ControlType.CUSTOM


def _clamp01(v: float) -> float:
    if v < 0.0:
        return 0.0
    if v > 1.0:
        return 1.0
    return v


def _draw_mark(
    draw: ImageDraw.ImageDraw,
    image_size: Tuple[int, int],
    rect: NormalizedRectangle,
    label: str,
    *,
    font: ImageFont.ImageFont,
    font_size: int,
    stroke_width: int,
) -> None:
    w, h = image_size
    x = rect.x * w
    y = rect.y * h
    rw = rect.width * w
    rh = rect.height * h
    cx = x + rw / 2
    cy = y + rh / 2

    radius = max(font_size, 12)
    left = cx - radius
    top = cy - radius
    right = cx + radius
    bottom = cy + radius

    fill = (255, 69, 0, 220)  # orangered-ish
    outline = (0, 0, 0, 255)
    text_fill = (255, 255, 255, 255)

    draw.ellipse([left, top, right, bottom], fill=fill, outline=outline, width=stroke_width)

    # Center text
    bbox = draw.textbbox((0, 0), label, font=font)
    tw = bbox[2] - bbox[0]
    th = bbox[3] - bbox[1]
    tx = cx - tw / 2
    ty = cy - th / 2
    draw.text((tx, ty), label, fill=text_fill, font=font)


