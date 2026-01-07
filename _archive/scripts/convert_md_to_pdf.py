"""
Markdown을 PDF로 변환하는 스크립트 (Playwright 사용)
Mermaid 다이어그램을 이미지로 렌더링하여 포함

요구사항:
    pip install markdown playwright
    playwright install chromium
    
사용법:
    python scripts/convert_md_to_pdf.py
"""

import re
import os
from pathlib import Path
import markdown
from playwright.sync_api import sync_playwright
import time
import base64
import urllib.parse


def embed_images_as_base64(md_content: str) -> str:
    """
    Markdown의 file:// 이미지 경로를 base64 data URI로 변환
    
    Args:
        md_content: Markdown 원본 내용
        
    Returns:
        이미지가 base64로 임베드된 Markdown
    """
    def replace_image(match):
        alt_text = match.group(1)
        img_url = match.group(2)
        
        # file:// URL 파싱
        if img_url.startswith('file:///'):
            # URL 디코딩
            decoded_path = urllib.parse.unquote(img_url.replace('file:///', ''))
            img_path = Path(decoded_path)
            
            if img_path.exists():
                # 파일 읽기 및 base64 인코딩
                with open(img_path, 'rb') as f:
                    img_data = base64.b64encode(f.read()).decode()
                
                # MIME 타입 결정
                ext = img_path.suffix.lower()
                mime_types = {
                    '.png': 'image/png',
                    '.jpg': 'image/jpeg',
                    '.jpeg': 'image/jpeg',
                    '.gif': 'image/gif',
                    '.svg': 'image/svg+xml'
                }
                mime_type = mime_types.get(ext, 'image/png')
                
                # data URI로 변환
                data_uri = f"data:{mime_type};base64,{img_data}"
                return f"![{alt_text}]({data_uri})"
            else:
                print(f"⚠️  이미지를 찾을 수 없음: {img_path}")
                return match.group(0)
        
        # file:// 경로가 아니면 그대로 반환
        return match.group(0)
    
    # Markdown 이미지 패턴 ![alt](url)
    pattern = r'!\[(.*?)\]\((.*?)\)'
    result = re.sub(pattern, replace_image, md_content)
    
    return result


def extract_and_replace_mermaid(md_content: str, diagram_images_dir: Path) -> str:
    """
    Mermaid 코드 블록을 이미지 태그로 교체
    
    Args:
        md_content: Markdown 원본 내용
        diagram_images_dir: 다이어그램 이미지 디렉토리
        
    Returns:
        Mermaid 블록이 이미지로 교체된 Markdown
    """
    def replace_mermaid_block(match):
        # 이미지 파일 찾기 (번호 기반)
        block_index = replace_mermaid_block.counter
        replace_mermaid_block.counter += 1
        
        # SVG 파일 찾기 (존재하면 SVG, 없으면 PNG)
        svg_files = list(diagram_images_dir.glob(f"{block_index:02d}_*.svg"))
        png_files = list(diagram_images_dir.glob(f"{block_index:02d}_*.png"))
        
        if svg_files:
            img_path = svg_files[0].resolve()
        elif png_files:
            img_path = png_files[0].resolve()
        else:
            return f"\n**[Diagram {block_index} - Image not found]**\n"
        
        # 절대 경로를 file:// URL로 변환
        img_url = img_path.as_uri()
        return f'\n![Diagram {block_index}]({img_url})\n'
    
    replace_mermaid_block.counter = 1
    
    # Mermaid 코드 블록 찾아서 교체
    pattern = r'```mermaid\n(.*?)```'
    result = re.sub(pattern, replace_mermaid_block, md_content, flags=re.DOTALL)
    
    return result


def markdown_to_html(md_content: str) -> str:
    """
    Markdown을 HTML로 변환 (확장 기능 포함)
    
    Args:
        md_content: Markdown 내용
        
    Returns:
        변환된 HTML
    """
    md = markdown.Markdown(extensions=[
        'extra',          # 테이블, 각주 등
        'codehilite',     # 코드 하이라이팅
        'toc',            # 목차
        'tables',         # 테이블
        'fenced_code',    # 펜스 코드 블록
    ])
    
    html_body = md.convert(md_content)
    
    # HTML 템플릿
    html_template = f"""
    <!DOCTYPE html>
    <html>
    <head>
        <meta charset="UTF-8">
        <style>
            @page {{
                size: A4;
                margin: 2cm;
            }}
            
            body {{
                font-family: 'Malgun Gothic', '맑은 고딕', Arial, sans-serif;
                font-size: 11pt;
                line-height: 1.7;
                color: #333;
                max-width: 100%;
                background: white;
            }}
            
            h1 {{
                color: #0078D4;
                font-size: 26pt;
                border-bottom: 3px solid #0078D4;
                padding-bottom: 12px;
                margin-top: 30px;
                page-break-after: avoid;
            }}
            
            h2 {{
                color: #2196F3;
                font-size: 20pt;
                border-bottom: 2px solid #2196F3;
                padding-bottom: 10px;
                margin-top: 25px;
                page-break-after: avoid;
            }}
            
            h3 {{
                color: #4CAF50;
                font-size: 15pt;
                margin-top: 20px;
                page-break-after: avoid;
            }}
            
            h4 {{
                color: #666;
                font-size: 13pt;
                margin-top: 15px;
            }}
            
            code {{
                background-color: #f5f5f5;
                padding: 3px 7px;
                border-radius: 4px;
                font-family: 'Consolas', 'Courier New', monospace;
                font-size: 10pt;
                color: #c7254e;
            }}
            
            pre {{
                background-color: #f8f8f8;
                border: 1px solid #ddd;
                border-radius: 5px;
                padding: 15px;
                overflow-x: auto;
                page-break-inside: avoid;
                margin: 15px 0;
            }}
            
            pre code {{
                background-color: transparent;
                padding: 0;
                color: #333;
            }}
            
            table {{
                border-collapse: collapse;
                width: 100%;
                margin: 20px 0;
                font-size: 10pt;
                page-break-inside: avoid;
            }}
            
            th {{
                background-color: #0078D4;
                color: white;
                padding: 12px;
                text-align: left;
                font-weight: bold;
            }}
            
            td {{
                border: 1px solid #ddd;
                padding: 10px;
            }}
            
            tr:nth-child(even) {{
                background-color: #f9f9f9;
            }}
            
            blockquote {{
                border-left: 4px solid #0078D4;
                margin: 20px 0;
                padding: 12px 24px;
                background-color: #f0f8ff;
                page-break-inside: avoid;
            }}
            
            img {{
                max-width: 100%;
                height: auto;
                display: block;
                margin: 25px auto;
                page-break-inside: avoid;
                border: 1px solid #ddd;
                border-radius: 5px;
                padding: 10px;
                background: white;
            }}
            
            a {{
                color: #0078D4;
                text-decoration: none;
            }}
            
            a[href^="file://"] {{
                color: #666;
                font-style: italic;
            }}
            
            ul, ol {{
                margin: 12px 0;
                padding-left: 35px;
            }}
            
            li {{
                margin: 6px 0;
            }}
            
            hr {{
                border: none;
                border-top: 2px solid #ddd;
                margin: 30px 0;
            }}
            
            /* 코드 하이라이팅 */
            .codehilite {{ background: #f8f8f8; padding: 10px; border-radius: 4px; }}
            .codehilite .hll {{ background-color: #ffffcc }}
            .codehilite .c {{ color: #408080; font-style: italic }}
            .codehilite .k {{ color: #008000; font-weight: bold }}
            .codehilite .o {{ color: #666666 }}
            .codehilite .cm {{ color: #408080; font-style: italic }}
            .codehilite .cp {{ color: #BC7A00 }}
            .codehilite .c1 {{ color: #408080; font-style: italic }}
            .codehilite .cs {{ color: #408080; font-style: italic }}
            .codehilite .kc {{ color: #008000; font-weight: bold }}
            .codehilite .kd {{ color: #008000; font-weight: bold }}
            .codehilite .kn {{ color: #008000; font-weight: bold }}
            .codehilite .kp {{ color: #008000 }}
            .codehilite .kr {{ color: #008000; font-weight: bold }}
            .codehilite .kt {{ color: #B00040 }}
            .codehilite .m {{ color: #666666 }}
            .codehilite .s {{ color: #BA2121 }}
            .codehilite .na {{ color: #7D9029 }}
            .codehilite .nb {{ color: #008000 }}
            .codehilite .nc {{ color: #0000FF; font-weight: bold }}
            .codehilite .no {{ color: #880000 }}
            .codehilite .nd {{ color: #AA22FF }}
            .codehilite .ni {{ color: #999999; font-weight: bold }}
            .codehilite .ne {{ color: #D2413A; font-weight: bold }}
            .codehilite .nf {{ color: #0000FF }}
            .codehilite .nl {{ color: #A0A000 }}
            .codehilite .nn {{ color: #0000FF; font-weight: bold }}
            .codehilite .nt {{ color: #008000; font-weight: bold }}
            .codehilite .nv {{ color: #19177C }}
            .codehilite .ow {{ color: #AA22FF; font-weight: bold }}
            .codehilite .w {{ color: #bbbbbb }}
        </style>
    </head>
    <body>
        {html_body}
    </body>
    </html>
    """
    
    return html_template


def convert_md_to_pdf(md_file: Path, output_file: Path, diagram_dir: Path = None):
    """
    Markdown 파일을 PDF로 변환 (Playwright 사용)
    
    Args:
        md_file: 입력 Markdown 파일
        output_file: 출력 PDF 파일
        diagram_dir: 다이어그램 이미지 디렉토리 (선택)
    """
    print(f"📄 Reading: {md_file.name}")
    
    # Markdown 파일 읽기
    with open(md_file, 'r', encoding='utf-8') as f:
        md_content = f.read()
    
    # Mermaid 다이어그램을 이미지로 교체
    if diagram_dir and diagram_dir.exists():
        print(f"🎨 Replacing Mermaid diagrams with images...")
        md_content = extract_and_replace_mermaid(md_content, diagram_dir)
    
    # ⭐ 이미지를 base64로 임베드 (file:// 경로 → data URI)
    print(f"🖼️  Embedding images as base64...")
    md_content = embed_images_as_base64(md_content)
    
    # Markdown → HTML 변환
    print(f"🔄 Converting to HTML...")
    html_content = markdown_to_html(md_content)
    
    # HTML → PDF 변환 (Playwright)
    print(f"📝 Generating PDF...")
    
    with sync_playwright() as p:
        browser = p.chromium.launch(headless=True)
        page = browser.new_page()
        
        # HTML 로드
        page.set_content(html_content, wait_until='networkidle')
        time.sleep(2)  # 이미지 렌더링 대기 시간 증가
        
        # PDF 생성
        page.pdf(
            path=str(output_file),
            format='A4',
            margin={
                'top': '2cm',
                'right': '2cm',
                'bottom': '2cm',
                'left': '2cm'
            },
            print_background=True,
            display_header_footer=True,
            header_template='<div></div>',
            footer_template='''
                <div style="font-size: 9pt; text-align: center; width: 100%; color: #666;">
                    <span class="pageNumber"></span> / <span class="totalPages"></span>
                </div>
            '''
        )
        
        browser.close()
    
    file_size = output_file.stat().st_size / 1024  # KB
    print(f"✅ PDF created: {file_size:.1f} KB")



def main():
    """메인 실행 함수"""
    # 경로 설정
    script_dir = Path(__file__).parent
    project_root = script_dir.parent
    brain_dir = Path(r"C:\Users\Yoon taegwan\.gemini\antigravity\brain\75e82786-b011-4ecc-9fa4-3069110aed2a")
    
    # 출력 디렉토리
    output_dir = project_root / "docs" / "pdf"
    output_dir.mkdir(parents=True, exist_ok=True)
    
    # 다이어그램 디렉토리
    diagram_dir = project_root / "docs" / "diagrams"
    
    # 변환할 파일들
    files_to_convert = [
        {
            "input": brain_dir / "dxnavis_analysis.md",
            "output": output_dir / "DXnavis_분석_리포트.pdf",
            "use_diagrams": False  # DXnavis 분석에는 Mermaid 다이어그램 없음
        },
        {
            "input": brain_dir / "system_flow.md",
            "output": output_dir / "시스템_플로우.pdf",
            "use_diagrams": True  # 시스템 플로우에는 Mermaid 다이어그램 있음
        },
        {
            "input": brain_dir / "technical_report_complete.md",
            "output": output_dir / "기술개발보고서_BIM통합플랫폼.pdf",
            "use_diagrams": False  # UI 이미지는 file:// 링크로 참조됨
        }
    ]
    
    print()
    print("=" * 70)
    print("  📚 Markdown to PDF Converter (Playwright)")
    print("=" * 70)
    print()
    
    for file_info in files_to_convert:
        input_file = file_info["input"]
        output_file = file_info["output"]
        use_diagrams = file_info["use_diagrams"]
        
        if not input_file.exists():
            print(f"⚠️  Skipping: {input_file.name} (file not found)")
            print()
            continue
        
        print(f"📝 Converting: {input_file.name}")
        print(f"   → Output: {output_file.name}")
        
        try:
            convert_md_to_pdf(
                input_file,
                output_file,
                diagram_dir if use_diagrams else None
            )
            print()
        except Exception as e:
            print(f"❌ Failed: {e}")
            import traceback
            traceback.print_exc()
            print()
    
    print("=" * 70)
    print(f"🎉 All PDFs saved to:")
    print(f"   {output_dir}")
    print("=" * 70)


if __name__ == "__main__":
    main()



def extract_and_replace_mermaid(md_content: str, diagram_images_dir: Path) -> str:
    """
    Mermaid 코드 블록을 이미지 태그로 교체
    
    Args:
        md_content: Markdown 원본 내용
        diagram_images_dir: 다이어그램 이미지 디렉토리
        
    Returns:
        Mermaid 블록이 이미지로 교체된 Markdown
    """
    def replace_mermaid_block(match):
        # 이미지 파일 찾기 (번호 기반)
        block_index = replace_mermaid_block.counter
        replace_mermaid_block.counter += 1
        
        # SVG 파일 찾기 (존재하면 SVG, 없으면 PNG)
        svg_files = list(diagram_images_dir.glob(f"{block_index:02d}_*.svg"))
        png_files = list(diagram_images_dir.glob(f"{block_index:02d}_*.png"))
        
        if svg_files:
            img_path = svg_files[0]
        elif png_files:
            img_path = png_files[0]
        else:
            return f"[Diagram {block_index} - Image not found]"
        
        # 이미지를 base64로 인코딩하여 임베드
        with open(img_path, 'rb') as f:
            img_data = base64.b64encode(f.read()).decode()
            ext = img_path.suffix[1:]  # .svg -> svg
            mime_type = 'svg+xml' if ext == 'svg' else 'png'
            
        return f'\n![Diagram {block_index}](data:image/{mime_type};base64,{img_data})\n'
    
    replace_mermaid_block.counter = 1
    
    # Mermaid 코드 블록 찾아서 교체
    pattern = r'```mermaid\n(.*?)```'
    result = re.sub(pattern, replace_mermaid_block, md_content, flags=re.DOTALL)
    
    return result


def markdown_to_html(md_content: str) -> str:
    """
    Markdown을 HTML로 변환 (확장 기능 포함)
    
    Args:
        md_content: Markdown 내용
        
    Returns:
        변환된 HTML
    """
    md = markdown.Markdown(extensions=[
        'extra',          # 테이블, 각주 등
        'codehilite',     # 코드 하이라이팅
        'toc',            # 목차
        'tables',         # 테이블
        'fenced_code',    # 펜스 코드 블록
    ])
    
    html_body = md.convert(md_content)
    
    # HTML 템플릿
    html_template = f"""
    <!DOCTYPE html>
    <html>
    <head>
        <meta charset="UTF-8">
        <style>
            @page {{
                size: A4;
                margin: 2cm;
                @bottom-right {{
                    content: "Page " counter(page) " of " counter(pages);
                    font-size: 9pt;
                    color: #666;
                }}
            }}
            
            body {{
                font-family: 'Malgun Gothic', '맑은 고딕', Arial, sans-serif;
                font-size: 10pt;
                line-height: 1.6;
                color: #333;
                max-width: 100%;
            }}
            
            h1 {{
                color: #0078D4;
                font-size: 24pt;
                border-bottom: 3px solid #0078D4;
                padding-bottom: 10px;
                margin-top: 30px;
                page-break-after: avoid;
            }}
            
            h2 {{
                color: #2196F3;
                font-size: 18pt;
                border-bottom: 2px solid #2196F3;
                padding-bottom: 8px;
                margin-top: 25px;
                page-break-after: avoid;
            }}
            
            h3 {{
                color: #4CAF50;
                font-size: 14pt;
                margin-top: 20px;
                page-break-after: avoid;
            }}
            
            h4 {{
                color: #666;
                font-size: 12pt;
                margin-top: 15px;
            }}
            
            code {{
                background-color: #f5f5f5;
                padding: 2px 6px;
                border-radius: 3px;
                font-family: 'Consolas', 'Courier New', monospace;
                font-size: 9pt;
                color: #c7254e;
            }}
            
            pre {{
                background-color: #f8f8f8;
                border: 1px solid #ddd;
                border-radius: 4px;
                padding: 12px;
                overflow-x: auto;
                page-break-inside: avoid;
            }}
            
            pre code {{
                background-color: transparent;
                padding: 0;
                color: #333;
            }}
            
            table {{
                border-collapse: collapse;
                width: 100%;
                margin: 15px 0;
                font-size: 9pt;
                page-break-inside: avoid;
            }}
            
            th {{
                background-color: #0078D4;
                color: white;
                padding: 10px;
                text-align: left;
                font-weight: bold;
            }}
            
            td {{
                border: 1px solid #ddd;
                padding: 8px;
            }}
            
            tr:nth-child(even) {{
                background-color: #f9f9f9;
            }}
            
            blockquote {{
                border-left: 4px solid #0078D4;
                margin: 15px 0;
                padding: 10px 20px;
                background-color: #f0f8ff;
                page-break-inside: avoid;
            }}
            
            img {{
                max-width: 100%;
                height: auto;
                display: block;
                margin: 20px auto;
                page-break-inside: avoid;
            }}
            
            a {{
                color: #0078D4;
                text-decoration: none;
            }}
            
            a:hover {{
                text-decoration: underline;
            }}
            
            ul, ol {{
                margin: 10px 0;
                padding-left: 30px;
            }}
            
            li {{
                margin: 5px 0;
            }}
            
            .page-break {{
                page-break-after: always;
            }}
            
            /* 코드 하이라이팅 */
            .codehilite .hll {{ background-color: #ffffcc }}
            .codehilite .c {{ color: #408080; font-style: italic }}
            .codehilite .k {{ color: #008000; font-weight: bold }}
            .codehilite .o {{ color: #666666 }}
            .codehilite .cm {{ color: #408080; font-style: italic }}
            .codehilite .cp {{ color: #BC7A00 }}
            .codehilite .c1 {{ color: #408080; font-style: italic }}
            .codehilite .cs {{ color: #408080; font-style: italic }}
            .codehilite .gd {{ color: #A00000 }}
            .codehilite .ge {{ font-style: italic }}
            .codehilite .gr {{ color: #FF0000 }}
            .codehilite .gh {{ color: #000080; font-weight: bold }}
            .codehilite .gi {{ color: #00A000 }}
            .codehilite .go {{ color: #888888 }}
            .codehilite .gp {{ color: #000080; font-weight: bold }}
            .codehilite .gs {{ font-weight: bold }}
            .codehilite .gu {{ color: #800080; font-weight: bold }}
            .codehilite .gt {{ color: #0044DD }}
            .codehilite .kc {{ color: #008000; font-weight: bold }}
            .codehilite .kd {{ color: #008000; font-weight: bold }}
            .codehilite .kn {{ color: #008000; font-weight: bold }}
            .codehilite .kp {{ color: #008000 }}
            .codehilite .kr {{ color: #008000; font-weight: bold }}
            .codehilite .kt {{ color: #B00040 }}
            .codehilite .m {{ color: #666666 }}
            .codehilite .s {{ color: #BA2121 }}
            .codehilite .na {{ color: #7D9029 }}
            .codehilite .nb {{ color: #008000 }}
            .codehilite .nc {{ color: #0000FF; font-weight: bold }}
            .codehilite .no {{ color: #880000 }}
            .codehilite .nd {{ color: #AA22FF }}
            .codehilite .ni {{ color: #999999; font-weight: bold }}
            .codehilite .ne {{ color: #D2413A; font-weight: bold }}
            .codehilite .nf {{ color: #0000FF }}
            .codehilite .nl {{ color: #A0A000 }}
            .codehilite .nn {{ color: #0000FF; font-weight: bold }}
            .codehilite .nt {{ color: #008000; font-weight: bold }}
            .codehilite .nv {{ color: #19177C }}
            .codehilite .ow {{ color: #AA22FF; font-weight: bold }}
            .codehilite .w {{ color: #bbbbbb }}
        </style>
    </head>
    <body>
        {html_body}
    </body>
    </html>
    """
    
    return html_template


def convert_md_to_pdf(md_file: Path, output_file: Path, diagram_dir: Path = None):
    """
    Markdown 파일을 PDF로 변환
    
    Args:
        md_file: 입력 Markdown 파일
        output_file: 출력 PDF 파일
        diagram_dir: 다이어그램 이미지 디렉토리 (선택)
    """
    print(f"📄 Reading: {md_file}")
    
    # Markdown 파일 읽기
    with open(md_file, 'r', encoding='utf-8') as f:
        md_content = f.read()
    
    # Mermaid 다이어그램을 이미지로 교체
    if diagram_dir and diagram_dir.exists():
        print(f"🎨 Replacing Mermaid diagrams with images from: {diagram_dir}")
        md_content = extract_and_replace_mermaid(md_content, diagram_dir)
    
    # Markdown → HTML 변환
    print(f"🔄 Converting Markdown to HTML...")
    html_content = markdown_to_html(md_content)
    
    # HTML → PDF 변환
    print(f"📝 Generating PDF...")
    font_config = FontConfiguration()
    
    HTML(string=html_content).write_pdf(
        output_file,
        font_config=font_config
    )
    
    file_size = output_file.stat().st_size / 1024  # KB
    print(f"✅ PDF created: {output_file} ({file_size:.1f} KB)")


def main():
    """메인 실행 함수"""
    # 경로 설정
    script_dir = Path(__file__).parent
    project_root = script_dir.parent
    brain_dir = Path(r"C:\Users\Yoon taegwan\.gemini\antigravity\brain\75e82786-b011-4ecc-9fa4-3069110aed2a")
    
    # 출력 디렉토리
    output_dir = project_root / "docs" / "pdf"
    output_dir.mkdir(parents=True, exist_ok=True)
    
    # 다이어그램 디렉토리
    diagram_dir = project_root / "docs" / "diagrams"
    
    # 변환할 파일들
    files_to_convert = [
        {
            "input": brain_dir / "dxnavis_analysis.md",
            "output": output_dir / "DXnavis_분석_리포트.pdf",
            "use_diagrams": False  # DXnavis 분석에는 다이어그램 없음
        },
        {
            "input": brain_dir / "system_flow.md",
            "output": output_dir / "시스템_플로우.pdf",
            "use_diagrams": True  # 시스템 플로우에는 Mermaid 다이어그램 있음
        }
    ]
    
    print("=" * 60)
    print("📚 Markdown to PDF Converter")
    print("=" * 60)
    print()
    
    for file_info in files_to_convert:
        input_file = file_info["input"]
        output_file = file_info["output"]
        use_diagrams = file_info["use_diagrams"]
        
        if not input_file.exists():
            print(f"⚠️  Skipping: {input_file.name} (file not found)")
            print()
            continue
        
        try:
            convert_md_to_pdf(
                input_file,
                output_file,
                diagram_dir if use_diagrams else None
            )
            print()
        except Exception as e:
            print(f"❌ Failed to convert {input_file.name}: {e}")
            print()
    
    print("=" * 60)
    print(f"🎉 All PDFs saved to: {output_dir}")
    print("=" * 60)


if __name__ == "__main__":
    main()
