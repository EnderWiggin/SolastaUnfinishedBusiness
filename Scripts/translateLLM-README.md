## Overview
This script provides automated batch translation for game localization files using an LLM-backed translation pipeline.  
It significantly improves translation quality, preserves formatting codes, and supports translating either an entire language folder or a single file.

## Features
- Batch translation using an OpenAI-compatible API
- Automatic preservation of formatting codes (`{0}`, `{1}`, `\n`, etc.)
- Concurrent translation with progress bars
- Supports translating:
    - an entire language directory
    - a single localization file
- Optional forced retranslation of all entries
- Optional language-specific translation targets

## Usage Examples

### Translate an entire language folder
Translates all files under `Translations/en/` to every other available language:
```bash
python translateLLM.py en
```

### Translate a single file
```bash
python translateLLM.py en/Backgrounds-en.txt
```

### Force retranslation (overwrite existing translations)
```bash
python translateLLM.py en --force
```

### Translate only to specific target languages
```bash
python translateLLM.py zh-CN --target en
python translateLLM.py zh-CN --target en,de,fr
```

### General form
```bash
python translateLLM.py <input_path> [--force] [--target LANGS]
```
