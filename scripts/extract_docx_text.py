#!/usr/bin/env python3
import sys
import zipfile
import xml.etree.ElementTree as ET

NS = {'w': 'http://schemas.openxmlformats.org/wordprocessingml/2006/main'}

def extract_text(docx_path: str) -> str:
    with zipfile.ZipFile(docx_path) as z:
        with z.open('word/document.xml') as f:
            tree = ET.parse(f)
    root = tree.getroot()
    paras = []
    for p in root.findall('.//w:body/w:p', NS):
        texts = []
        for t in p.findall('.//w:t', NS):
            texts.append(t.text or '')
        # handle breaks (optional)
        if texts:
            paras.append(''.join(texts))
    return '\n'.join(paras)

if __name__ == '__main__':
    if len(sys.argv) < 2:
        print('Usage: extract_docx_text.py <file.docx>', file=sys.stderr)
        sys.exit(1)
    try:
        sys.stdout.reconfigure(encoding='utf-8', errors='replace')
    except Exception:
        pass
    print(extract_text(sys.argv[1]))
