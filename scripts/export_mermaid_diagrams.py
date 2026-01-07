"""
Mermaid 다이어그램을 고품질 이미지로 자동 변환하는 스크립트

요구사항:
    pip install playwright
    playwright install chromium

사용법:
    python scripts/export_mermaid_diagrams.py
    
    SVG와 고해상도 PNG 모두 생성됩니다.
"""

import re
import os
from pathlib import Path
from playwright.sync_api import sync_playwright
import time


def extract_mermaid_blocks(markdown_file: Path) -> list[tuple[str, str]]:
    """
    마크다운 파일에서 Mermaid 코드 블록 추출
    
    Returns:
        List of (title, mermaid_code) tuples
    """
    with open(markdown_file, 'r', encoding='utf-8') as f:
        content = f.read()
    
    # 제목과 Mermaid 블록 매칭
    pattern = r'###?\s+(.+?)\n\n```mermaid\n(.*?)```'
    matches = re.findall(pattern, content, re.DOTALL)
    
    diagrams = []
    for i, (title, code) in enumerate(matches, 1):
        # 제목을 파일명으로 변환 (특수문자 제거)
        clean_title = re.sub(r'[^\w\s-]', '', title).strip()
        clean_title = re.sub(r'[-\s]+', '_', clean_title)
        filename = f"{i:02d}_{clean_title}"
        diagrams.append((filename, code.strip()))
    
    return diagrams


def render_mermaid_to_svg(mermaid_code: str, output_path: Path):
    """
    Mermaid 코드를 SVG 파일로 렌더링 (벡터, 무한 확대 가능)
    
    Args:
        mermaid_code: Mermaid 다이어그램 코드
        output_path: 출력 SVG 파일 경로
    """
    html_template = f"""
    <!DOCTYPE html>
    <html>
    <head>
        <meta charset="UTF-8">
        <script src="https://cdn.jsdelivr.net/npm/mermaid@10/dist/mermaid.min.js"></script>
        <style>
            body {{
                margin: 40px;
                padding: 0;
                background: white;
                font-family: 'Segoe UI', Arial, sans-serif;
            }}
            #mermaid-diagram {{
                display: inline-block;
            }}
        </style>
    </head>
    <body>
        <div id="mermaid-diagram" class="mermaid">
{mermaid_code}
        </div>
        <script>
            mermaid.initialize({{ 
                startOnLoad: true,
                theme: 'default',
                themeVariables: {{
                    fontSize: '18px',
                    fontFamily: 'Segoe UI, Arial, sans-serif'
                }},
                flowchart: {{
                    htmlLabels: true,
                    curve: 'basis'
                }}
            }});
        </script>
    </body>
    </html>
    """
    
    with sync_playwright() as p:
        browser = p.chromium.launch(headless=True)
        page = browser.new_page(viewport={'width': 2560, 'height': 1440})
        page.set_content(html_template)
        
        # Mermaid 렌더링 대기
        page.wait_for_selector('#mermaid-diagram svg', timeout=15000)
        time.sleep(0.5)  # 추가 렌더링 대기
        
        # SVG 전체 내용 추출
        svg_content = page.evaluate('''() => {
            const svg = document.querySelector('#mermaid-diagram svg');
            const serializer = new XMLSerializer();
            return serializer.serializeToString(svg);
        }''')
        
        # SVG 파일로 저장
        with open(output_path, 'w', encoding='utf-8') as f:
            f.write('<?xml version="1.0" encoding="UTF-8"?>\n')
            f.write(svg_content)
        
        browser.close()


def render_mermaid_to_png(mermaid_code: str, output_path: Path, scale: float = 3.0):
    """
    Mermaid 코드를 고해상도 PNG 파일로 렌더링
    
    Args:
        mermaid_code: Mermaid 다이어그램 코드
        output_path: 출력 PNG 파일 경로
        scale: 해상도 배율 (기본 3.0 = Full HD x3)
    """
    html_template = f"""
    <!DOCTYPE html>
    <html>
    <head>
        <meta charset="UTF-8">
        <script src="https://cdn.jsdelivr.net/npm/mermaid@10/dist/mermaid.min.js"></script>
        <style>
            body {{
                margin: 40px;
                padding: 0;
                background: white;
                font-family: 'Segoe UI', Arial, sans-serif;
            }}
            #mermaid-diagram {{
                display: inline-block;
            }}
        </style>
    </head>
    <body>
        <div id="mermaid-diagram" class="mermaid">
{mermaid_code}
        </div>
        <script>
            mermaid.initialize({{ 
                startOnLoad: true,
                theme: 'default',
                themeVariables: {{
                    fontSize: '18px',
                    fontFamily: 'Segoe UI, Arial, sans-serif'
                }},
                flowchart: {{
                    htmlLabels: true,
                    curve: 'basis'
                }}
            }});
        </script>
    </body>
    </html>
    """
    
    with sync_playwright() as p:
        browser = p.chromium.launch(headless=True)
        # 고해상도 viewport
        page = browser.new_page(
            viewport={'width': int(2560 * scale), 'height': int(1440 * scale)},
            device_scale_factor=scale
        )
        page.set_content(html_template)
        
        # Mermaid 렌더링 대기
        page.wait_for_selector('#mermaid-diagram svg', timeout=15000)
        time.sleep(0.5)  # 추가 렌더링 대기
        
        # SVG 요소 스크린샷
        svg_element = page.query_selector('#mermaid-diagram svg')
        svg_element.screenshot(path=str(output_path), scale='device')
        
        browser.close()


def main():
    """메인 실행 함수"""
    # 경로 설정
    script_dir = Path(__file__).parent
    project_root = script_dir.parent
    brain_dir = Path(r"C:\Users\Yoon taegwan\.gemini\antigravity\brain\75e82786-b011-4ecc-9fa4-3069110aed2a")
    
    markdown_file = brain_dir / "system_flow.md"
    output_dir = project_root / "docs" / "diagrams"
    output_dir.mkdir(parents=True, exist_ok=True)
    
    print(f"📄 Reading: {markdown_file}")
    
    # Mermaid 블록 추출
    diagrams = extract_mermaid_blocks(markdown_file)
    print(f"✅ Found {len(diagrams)} diagrams\n")
    
    print("🎨 Generating high-quality images...\n")
    
    # 각 다이어그램 변환 (SVG + PNG 모두 생성)
    for filename, code in diagrams:
        print(f"  [{filename}]")
        
        # SVG 생성 (벡터, 무한 확대)
        svg_file = output_dir / f"{filename}.svg"
        try:
            print(f"    🔷 Rendering SVG...", end=' ')
            render_mermaid_to_svg(code, svg_file)
            print(f"✅ ({svg_file.stat().st_size // 1024} KB)")
        except Exception as e:
            print(f"❌ Failed: {e}")
        
        # PNG 생성 (고해상도)
        png_file = output_dir / f"{filename}.png"
        try:
            print(f"    🔶 Rendering PNG (3x)...", end=' ')
            render_mermaid_to_png(code, png_file, scale=3.0)
            print(f"✅ ({png_file.stat().st_size // 1024} KB)")
        except Exception as e:
            print(f"❌ Failed: {e}")
        
        print()
    
    print(f"🎉 All diagrams exported to: {output_dir}")
    print(f"\n� Summary:")
    print(f"  - SVG files: Vector graphics (infinite zoom)")
    print(f"  - PNG files: 3x resolution (high quality)")


if __name__ == "__main__":
    main()
