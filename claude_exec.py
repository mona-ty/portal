#!/usr/bin/env python3
"""
Claude APIをコマンドラインから呼び出すスクリプト
使用方法: python claude_exec.py "プロンプト"
"""

import sys
import os
from anthropic import Anthropic

def main():
    if len(sys.argv) < 2:
        print("使用方法: python claude_exec.py \"プロンプト\"")
        sys.exit(1)
    
    prompt = sys.argv[1]
    
    # APIキーを環境変数から取得
    api_key = os.getenv('ANTHROPIC_API_KEY')
    if not api_key:
        print("エラー: ANTHROPIC_API_KEY環境変数が設定されていません")
        print("設定方法: set ANTHROPIC_API_KEY=your_api_key_here")
        sys.exit(1)
    
    try:
        client = Anthropic(api_key=api_key)
        
        response = client.messages.create(
            model="claude-3-5-sonnet-20241022",
            max_tokens=4000,
            messages=[
                {
                    "role": "user",
                    "content": prompt
                }
            ]
        )
        
        print(response.content[0].text)
        
    except Exception as e:
        print(f"エラー: {e}")
        sys.exit(1)

if __name__ == "__main__":
    main()
