// Windows-only helper to auto-detect the FF14 submarine list region
// Strategy:
// 1) Capture the FF14 window to a BMP
// 2) Run Tesseract CLI to output TSV (jpn+eng, --psm 6)
// 3) Parse TSV, find lines containing "Rank" or time hints (分/時間 or digits)
// 4) Compute a bounding box across matched lines; output absolute screen coords as JSON

#define UNICODE
#define _UNICODE
#include <windows.h>
#include <stdio.h>
#include <wchar.h>
#include <stdbool.h>
#include <stdlib.h>
#include <string.h>
#include <stdint.h>

typedef struct { int x, y, w, h; } Rect;

static BOOL find_ff14_window(HWND *out_hwnd, RECT *out_rect) {
    HWND found = NULL;
    RECT r = {0};

    EnumWindows(
        [](HWND hwnd, LPARAM lp) -> BOOL {
            int length = GetWindowTextLengthW(hwnd);
            if (length <= 0) return TRUE;
            WCHAR buf[512];
            GetWindowTextW(hwnd, buf, (int)(sizeof(buf)/sizeof(buf[0])));
            // Heuristics: title contains FF14 names (EN/JP)
            if (wcsstr(buf, L"FINAL FANTASY XIV") || wcsstr(buf, L"ファイナルファンタジーXIV") || wcsstr(buf, L"FFXIV")) {
                // Must be visible
                if (IsWindowVisible(hwnd)) {
                    HWND *store = (HWND*)lp;
                    *store = hwnd;
                    return FALSE; // stop enum
                }
            }
            return TRUE;
        },
        (LPARAM)&found);

    if (!found) return FALSE;
    if (!GetWindowRect(found, &r)) return FALSE;
    *out_hwnd = found;
    *out_rect = r;
    return TRUE;
}

static BOOL save_hbitmap_to_bmp(HBITMAP hbmp, int width, int height, const wchar_t *path) {
    BITMAP bmp;
    if (!GetObject(hbmp, sizeof(BITMAP), &bmp)) return FALSE;

    BITMAPINFOHEADER bi;
    ZeroMemory(&bi, sizeof(bi));
    bi.biSize = sizeof(BITMAPINFOHEADER);
    bi.biWidth = width;
    bi.biHeight = -height; // top-down
    bi.biPlanes = 1;
    bi.biBitCount = 24; // RGB
    bi.biCompression = BI_RGB;

    int rowSize = ((bi.biBitCount * width + 31) / 32) * 4;
    int dataSize = rowSize * height;
    uint8_t *data = (uint8_t*)malloc(dataSize);
    if (!data) return FALSE;

    HDC hdc = GetDC(NULL);
    BOOL ok = GetDIBits(hdc, hbmp, 0, height, data, (BITMAPINFO*)&bi, DIB_RGB_COLORS);
    ReleaseDC(NULL, hdc);
    if (!ok) { free(data); return FALSE; }

    BITMAPFILEHEADER bfh;
    bfh.bfType = 0x4D42; // 'BM'
    bfh.bfOffBits = sizeof(BITMAPFILEHEADER) + sizeof(BITMAPINFOHEADER);
    bfh.bfSize = bfh.bfOffBits + dataSize;
    bfh.bfReserved1 = 0;
    bfh.bfReserved2 = 0;

    HANDLE hFile = CreateFileW(path, GENERIC_WRITE, 0, NULL, CREATE_ALWAYS, FILE_ATTRIBUTE_NORMAL, NULL);
    if (hFile == INVALID_HANDLE_VALUE) { free(data); return FALSE; }
    DWORD written = 0;
    WriteFile(hFile, &bfh, sizeof(bfh), &written, NULL);
    WriteFile(hFile, &bi, sizeof(bi), &written, NULL);
    WriteFile(hFile, data, dataSize, &written, NULL);
    CloseHandle(hFile);
    free(data);
    return TRUE;
}

static BOOL capture_window_bmp(HWND hwnd, const wchar_t *path, int *outW, int *outH) {
    RECT wr; if (!GetWindowRect(hwnd, &wr)) return FALSE;
    int w = wr.right - wr.left;
    int h = wr.bottom - wr.top;
    HDC hdcWindow = GetWindowDC(hwnd);
    HDC hdcMem = CreateCompatibleDC(hdcWindow);
    HBITMAP hbmp = CreateCompatibleBitmap(hdcWindow, w, h);
    HGDIOBJ old = SelectObject(hdcMem, hbmp);

    BOOL ok = FALSE;
    // Try PrintWindow first
    if (PrintWindow(hwnd, hdcMem, 0)) {
        ok = TRUE;
    } else {
        // Fallback BitBlt
        ok = BitBlt(hdcMem, 0, 0, w, h, hdcWindow, 0, 0, SRCCOPY);
    }

    SelectObject(hdcMem, old);
    ReleaseDC(hwnd, hdcWindow);

    if (!ok) { DeleteObject(hbmp); DeleteDC(hdcMem); return FALSE; }

    BOOL saved = save_hbitmap_to_bmp(hbmp, w, h, path);
    DeleteObject(hbmp);
    DeleteDC(hdcMem);
    if (saved) { if (outW) *outW = w; if (outH) *outH = h; }
    return saved;
}

static BOOL run_tesseract_make_tsv(const wchar_t *imgPath, wchar_t *tsvOutPath, size_t tsvOutLen) {
    wchar_t tempDir[MAX_PATH];
    GetTempPathW(MAX_PATH, tempDir);
    wchar_t base[MAX_PATH];
    swprintf(base, MAX_PATH, L"%sff14_auto_%lu", tempDir, GetTickCount());
    wchar_t cmd[2048];
    // tesseract <input> <outputbase> -l jpn+eng --psm 6 tsv
    swprintf(cmd, 2048, L"\"%s\" \"%s\" \"%s\" -l jpn+eng --psm 6 tsv",
             L"tesseract", imgPath, base);

    STARTUPINFOW si = {0};
    PROCESS_INFORMATION pi = {0};
    si.cb = sizeof(si);
    si.dwFlags = STARTF_USESHOWWINDOW;
    si.wShowWindow = SW_HIDE;

    wchar_t cmdline[2048];
    wcsncpy(cmdline, cmd, 2047);
    cmdline[2047] = 0;

    BOOL ok = CreateProcessW(NULL, cmdline, NULL, NULL, FALSE, CREATE_NO_WINDOW, NULL, NULL, &si, &pi);
    if (!ok) return FALSE;

    WaitForSingleObject(pi.hProcess, 120000); // wait up to 2 min
    CloseHandle(pi.hThread);
    CloseHandle(pi.hProcess);

    swprintf(tsvOutPath, (int)tsvOutLen, L"%s.tsv", base);
    return TRUE;
}

static bool parse_tsv_find_roi(const char *tsv, Rect *out) {
    // TSV columns: level page_num block_num par_num line_num word_num left top width height conf text
    // We'll group by line_num if any word in that line matches pattern; then union across lines.
    int minL = INT32_MAX, minT = INT32_MAX, maxR = -1, maxB = -1;
    char *buf = _strdup(tsv);
    if (!buf) return false;
    char *saveptr = NULL;
    char *line = strtok_s(buf, "\n", &saveptr);
    // skip header
    if (line) line = strtok_s(NULL, "\n", &saveptr);
    while (line) {
        // Copy line to mutate
        char *ln = line;
        // Split into columns by tab
        // We'll extract: line_num (4), left(6), top(7), width(8), height(9), text(11)
        int col = 0; char *p = ln; char *start = ln;
        int line_num = -1; int left=0, top=0, width=0, height=0; const char *text = NULL;
        for (; *p; ++p) {
            if (*p == '\t') { *p = 0; // end field
                // process field in start..p-1
                if (col == 4) line_num = atoi(start);
                else if (col == 6) left = atoi(start);
                else if (col == 7) top = atoi(start);
                else if (col == 8) width = atoi(start);
                else if (col == 9) height = atoi(start);
                // move to next
                col++; start = p+1;
            }
        }
        // last column (text)
        if (start && *start) text = start;

        bool match = false;
        if (text && *text) {
            // Simple heuristics: contains "Rank" or any Japanese time hint
            if (strstr(text, "Rank")) match = true;
            // UTF-8 bytes for "分" and "時間" will be present; do naive strstr on UTF-8
            if (!match && strstr(text, "\xE5\x88\x86")) match = true; // 分
            if (!match && strstr(text, "\xE6\x99\x82\xE9\x96\x93")) match = true; // 時間
            // Or digits-only tokens likely to be minutes (very loose)
            if (!match) {
                bool hasDigit=false; for (const char *q=text; *q; ++q) if (*q>='0' && *q<='9') { hasDigit=true; break; }
                if (hasDigit) match = true;
            }
        }

        if (match && width>0 && height>0) {
            int L = left, T = top, R = left + width, B = top + height;
            if (L < minL) minL = L; if (T < minT) minT = T;
            if (R > maxR) maxR = R; if (B > maxB) maxB = B;
        }

        line = strtok_s(NULL, "\n", &saveptr);
    }
    free(buf);
    if (maxR <= 0 || maxB <= 0) return false;
    // pad a bit
    int pad = 8;
    out->x = (minL > pad ? minL - pad : 0);
    out->y = (minT > pad ? minT - pad : 0);
    out->w = (maxR - minL) + 2*pad;
    out->h = (maxB - minT) + 2*pad;
    return true;
}

static bool detect_once(Rect *absOut) {
    HWND hwnd; RECT wr;
    if (!find_ff14_window(&hwnd, &wr)) return false;

    wchar_t imgPath[MAX_PATH];
    GetTempPathW(MAX_PATH, imgPath);
    wchar_t imgFile[MAX_PATH];
    swprintf(imgFile, MAX_PATH, L"%sff14_auto_%lu.bmp", imgPath, GetTickCount());

    int w=0,h=0;
    if (!capture_window_bmp(hwnd, imgFile, &w, &h)) return false;

    wchar_t tsvPath[MAX_PATH];
    if (!run_tesseract_make_tsv(imgFile, tsvPath, MAX_PATH)) return false;

    // Read TSV
    FILE *f = _wfopen(tsvPath, L"rb");
    if (!f) return false;
    fseek(f, 0, SEEK_END); long sz = ftell(f); fseek(f, 0, SEEK_SET);
    char *buf = (char*)malloc(sz+1); if (!buf) { fclose(f); return false; }
    fread(buf, 1, sz, f); buf[sz] = 0; fclose(f);

    Rect roiWin;
    bool ok = parse_tsv_find_roi(buf, &roiWin);
    free(buf);

    // Cleanup temp files (best-effort)
    DeleteFileW(imgFile);
    DeleteFileW(tsvPath);

    if (!ok) return false;
    // Convert to absolute screen coords using window rect
    absOut->x = wr.left + roiWin.x;
    absOut->y = wr.top + roiWin.y;
    absOut->w = roiWin.w;
    absOut->h = roiWin.h;
    return true;
}

int wmain(int argc, wchar_t **argv) {
    bool watch = false; int intervalMs = 20000; int timeoutMs = 300000; // 20s, 5min
    for (int i=1;i<argc;i++) {
        if (wcscmp(argv[i], L"--watch") == 0) watch = true;
        else if (wcsncmp(argv[i], L"--interval=", 11) == 0) intervalMs = _wtoi(argv[i]+11);
        else if (wcsncmp(argv[i], L"--timeout=", 10) == 0) timeoutMs = _wtoi(argv[i]+10);
    }

    DWORD start = GetTickCount();
    while (1) {
        Rect r;
        if (detect_once(&r)) {
            // Print JSON to stdout
            printf("{\"x\":%d,\"y\":%d,\"width\":%d,\"height\":%d}\n", r.x, r.y, r.w, r.h);
            return 0;
        }
        if (!watch) break;
        if ((int)(GetTickCount() - start) > timeoutMs) break;
        Sleep(intervalMs);
    }
    fprintf(stderr, "auto_setup: region not detected\n");
    return 2;
}

