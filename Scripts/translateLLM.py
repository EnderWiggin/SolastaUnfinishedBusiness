#!/usr/bin/env python3
#
# AUTHOR: Translation Script using LLM
#
# DESCRIPTION: This script translates localization files from one language to all others
#              using an OpenAI-compatible API with concurrent translation support.
#
# REQUIRES:
#   - Python 3.9+
#   - openai library (pip install openai)
#   - python-dotenv library (pip install python-dotenv)
#   - rich library (pip install rich)
#
# USAGE:
#   python translateLLM.py <input_path> [--force] [--target LANG]
#   
#   Examples:
#     # Translate all files in a language folder (from Scripts directory)
#     python translateLLM.py en
#     
#     # Translate a single file (from Scripts directory)
#     python translateLLM.py en/Backgrounds-en.txt
#     
#     # Force retranslation of all entries (including existing ones)
#     python translateLLM.py en --force
#     
#     # Translate to specific target language(s) only
#     python translateLLM.py zh-CN --target en
#     python translateLLM.py zh-CN --target en,de,fr

import os
import sys
import codecs
import time
import argparse
from pathlib import Path
from typing import Dict, List, Tuple, Optional, Set
from dataclasses import dataclass, field
from queue import Queue
from concurrent.futures import ThreadPoolExecutor, as_completed
from threading import Lock
from collections import defaultdict
from dotenv import load_dotenv
from openai import OpenAI
from rich.console import Console
from rich.progress import Progress, TaskID, BarColumn, TextColumn, TimeRemainingColumn, SpinnerColumn
from rich.panel import Panel

# Load environment variables from .env file
load_dotenv(Path(__file__).parent / '.env')

# Configuration from environment variables
API_BASE_URL = os.getenv('API_BASE_URL', 'https://api.openai.com/v1')
API_KEY = os.getenv('API_KEY')
MODEL_ID = os.getenv('MODEL_ID', 'gpt-4')
BATCH_SIZE = int(os.getenv('BATCH_SIZE', '10'))
MAX_RETRIES = int(os.getenv('MAX_RETRIES', '3'))
RETRY_DELAY = int(os.getenv('RETRY_DELAY', '2'))
MAX_WORKERS = int(os.getenv('MAX_WORKERS', '3'))

# Validate configuration
if not API_KEY:
    print("ERROR: API_KEY not found in .env file. Please copy .env.example to .env and configure it.")
    sys.exit(1)

# Initialize OpenAI client
client = OpenAI(
    api_key=API_KEY,
    base_url=API_BASE_URL
)

# Console for rich output
console = Console()

# Language code mapping
LANGUAGE_NAMES = {
    'zh-CN': 'Simplified Chinese',
    'en': 'English',
    'de': 'German',
    'es': 'Spanish',
    'fr': 'French',
    'it': 'Italian',
    'ja': 'Japanese',
    'ko': 'Korean',
    'pt-BR': 'Brazilian Portuguese',
    'ru': 'Russian'
}


@dataclass
class TranslationBatch:
    """Represents a batch of translations for a specific file and target language."""
    source_file: str
    target_file: str
    source_lang: str
    target_lang: str
    batch_num: int
    total_batches: int
    entries: List[Tuple[str, str]]
    file_key: str  # Unique key for the file (source_file + target_lang)
    
    
@dataclass 
class FileTranslationData:
    """Holds accumulated translation data for a file."""
    target_file: str
    data: Dict[str, str] = field(default_factory=dict)
    lock: Lock = field(default_factory=Lock)
    completed_batches: int = 0
    total_batches: int = 0
    

class TranslationManager:
    """Manages concurrent translation tasks with progress tracking."""
    
    def __init__(self, max_workers: int):
        self.max_workers = max_workers
        self.file_data: Dict[str, FileTranslationData] = {}
        self.file_data_lock = Lock()
        
    def get_file_data(self, file_key: str, target_file: str, existing_data: Dict[str, str]) -> FileTranslationData:
        """Get or create FileTranslationData for a file."""
        with self.file_data_lock:
            if file_key not in self.file_data:
                self.file_data[file_key] = FileTranslationData(
                    target_file=target_file,
                    data=existing_data.copy()
                )
            return self.file_data[file_key]
    
    def save_file_if_complete(self, file_key: str) -> bool:
        """Save file if all batches are completed. Returns True if saved."""
        with self.file_data_lock:
            if file_key not in self.file_data:
                return False
            
            file_data = self.file_data[file_key]
            if file_data.completed_batches >= file_data.total_batches:
                write_localization_file(file_data.target_file, file_data.data)
                return True
        return False


def unpack_record(record: str) -> Tuple[str, str]:
    """
    Parse a single localization record into key and value.
    
    Args:
        record: A line from the localization file in format "key=value"
        
    Returns:
        Tuple of (key, value)
    """
    term = ""
    text = ""
    try:
        term, text = record.split("=", 1)
        text = text.strip()
    except ValueError:
        term = record
    
    return term, text if text != "" else "EMPTY"


def read_localization_file(filename: str) -> Dict[str, str]:
    """
    Read a localization file and return a dictionary of key-value pairs.
    
    Args:
        filename: Path to the localization file
        
    Returns:
        Dictionary mapping keys to their localized values
    """
    result = {}
    
    if not os.path.exists(filename):
        return result
    
    try:
        with open(filename, "rt", encoding="utf-8") as f:
            line_count = 0
            for line in f:
                # Remove BOM from first line if present
                if line_count == 0 and line.startswith(codecs.BOM_UTF8.decode("utf-8")):
                    line = line[1:]
                line_count += 1
                
                line = line.strip()
                if line:
                    term, text = unpack_record(line)
                    result[term] = text
    except Exception as e:
        console.print(f"[red]ERROR reading {filename}: {e}[/red]")
    
    return result


def write_localization_file(filename: str, data: Dict[str, str]):
    """
    Write localization data to a file, sorted by key.
    
    Args:
        filename: Path to the output file
        data: Dictionary of localization key-value pairs
    """
    # Create directory if it doesn't exist
    os.makedirs(os.path.dirname(filename), exist_ok=True)
    
    # Sort and write
    with open(filename, "wt", encoding="utf-8") as f:
        for key in sorted(data.keys()):
            f.write(f"{key}={data[key]}\n")


def translate_batch_api(entries: List[Tuple[str, str]], source_lang: str, target_lang: str) -> Dict[str, str]:
    """
    Translate a batch of entries using the LLM API.
    
    Args:
        entries: List of (key, text) tuples to translate
        source_lang: Source language name
        target_lang: Target language name
        
    Returns:
        Dictionary mapping keys to translated values
    """
    if not entries:
        return {}
    
    # Prepare the prompt
    entries_text = "\n".join([f"{i+1}. {key}={text}" for i, (key, text) in enumerate(entries)])
    
    prompt = f"""You are a professional translator for a video game localization project.
Translate the following game text entries from {source_lang} to {target_lang}.

IMPORTANT INSTRUCTIONS:
1. Preserve all special formatting codes like \\n (newline), {{0}}, {{1}}, etc.
2. Maintain the same key names (everything before the = sign)
3. Keep game-specific terms consistent
4. Return ONLY the translated entries in the exact same format: "key=translated_text"
5. Return each entry on a new line, numbered as shown below
6. Do not add explanations or comments

Entries to translate:
{entries_text}

Respond with the translated entries in the same numbered format."""

    # Make API call with retries
    for attempt in range(MAX_RETRIES):
        try:
            response = client.chat.completions.create(
                model=MODEL_ID,
                messages=[
                    {"role": "system", "content": "You are a professional game localization translator. You provide accurate translations while preserving all formatting codes and game terminology."},
                    {"role": "user", "content": prompt}
                ],
                temperature=0.3
            )
            
            # Parse the response
            translated_text = response.choices[0].message.content
            if not translated_text:
                raise ValueError("Empty response from API")
                
            result = {}
            
            for line in translated_text.strip().split('\n'):
                line = line.strip()
                # Remove numbering if present (e.g., "1. " or "1)")
                if line and (line[0].isdigit() or line.startswith('-')):
                    # Find the first occurrence of key=value pattern
                    equal_pos = line.find('=')
                    if equal_pos > 0:
                        # Extract everything before the number/bullet
                        key_start = 0
                        for i, char in enumerate(line):
                            if char.isalpha() or char == '/':
                                key_start = i
                                break
                        
                        key_value_part = line[key_start:]
                        try:
                            key, value = key_value_part.split('=', 1)
                            result[key.strip()] = value.strip()
                        except ValueError:
                            continue
            
            return result
            
        except Exception as e:
            if attempt < MAX_RETRIES - 1:
                time.sleep(RETRY_DELAY)
            else:
                console.print(f"[red]ERROR: Failed to translate batch after {MAX_RETRIES} attempts: {e}[/red]")
                return {}
    
    return {}


def process_translation_batch(batch: TranslationBatch, manager: TranslationManager, progress: Progress, task_id: TaskID) -> bool:
    """
    Process a single translation batch.
    
    Args:
        batch: The translation batch to process
        manager: Translation manager
        progress: Rich progress instance
        task_id: Progress task ID for this language
        
    Returns:
        True if successful
    """
    try:
        # Get file data
        file_data = manager.get_file_data(batch.file_key, batch.target_file, {})
        
        # Translate the batch
        source_lang_name = LANGUAGE_NAMES.get(batch.source_lang, batch.source_lang)
        target_lang_name = LANGUAGE_NAMES.get(batch.target_lang, batch.target_lang)
        
        translations = translate_batch_api(batch.entries, source_lang_name, target_lang_name)
        
        # Update file data
        with file_data.lock:
            for key, value in translations.items():
                if value:
                    file_data.data[key] = value
            file_data.completed_batches += 1
        
        # Update progress
        progress.update(task_id, advance=1)
        
        # Try to save file if all batches complete
        if manager.save_file_if_complete(batch.file_key):
            progress.console.print(f"[green]✓[/green] Saved: {Path(batch.target_file).name}")
        
        return True
        
    except Exception as e:
        console.print(f"[red]ERROR processing batch: {e}[/red]")
        return False


def create_translation_batches(
    source_files: List[str],
    source_lang_code: str,
    target_lang_codes: List[str],
    translations_dir: Path,
    force: bool
) -> Dict[str, List[TranslationBatch]]:
    """
    Create translation batches organized by target language.
    
    Args:
        source_files: List of source file paths
        source_lang_code: Source language code
        target_lang_codes: List of target language codes
        translations_dir: Base translations directory
        force: Whether to force retranslation
        
    Returns:
        Dictionary mapping language code to list of batches
    """
    language_batches: Dict[str, List[TranslationBatch]] = defaultdict(list)
    
    for source_file in source_files:
        source_path = Path(source_file)
        source_data = read_localization_file(source_file)
        
        if not source_data:
            continue
        
        # Get relative path from source language folder
        source_lang_dir = translations_dir / source_lang_code
        relative_path = source_path.relative_to(source_lang_dir)
        
        for target_lang_code in target_lang_codes:
            # Build target path
            target_lang_dir = translations_dir / target_lang_code
            target_filename = relative_path.stem.replace(f"-{source_lang_code}", f"-{target_lang_code}") + relative_path.suffix
            target_file = target_lang_dir / relative_path.parent / target_filename
            
            # Read existing target file
            target_data = read_localization_file(str(target_file))
            
            # Find entries that need translation
            to_translate = []
            for key, value in source_data.items():
                if value != "EMPTY":
                    if force or key not in target_data:
                        to_translate.append((key, value))
            
            if not to_translate:
                continue
            
            # Create batches for this file
            file_key = f"{source_file}::{target_lang_code}"
            total_batches = (len(to_translate) + BATCH_SIZE - 1) // BATCH_SIZE
            
            for i in range(0, len(to_translate), BATCH_SIZE):
                batch_entries = to_translate[i:i + BATCH_SIZE]
                batch_num = i // BATCH_SIZE + 1
                
                batch = TranslationBatch(
                    source_file=source_file,
                    target_file=str(target_file),
                    source_lang=source_lang_code,
                    target_lang=target_lang_code,
                    batch_num=batch_num,
                    total_batches=total_batches,
                    entries=batch_entries,
                    file_key=file_key
                )
                
                language_batches[target_lang_code].append(batch)
    
    return language_batches


def run_concurrent_translation(
    language_batches: Dict[str, List[TranslationBatch]],
    manager: TranslationManager
):
    """
    Run concurrent translation with progress tracking.
    
    Args:
        language_batches: Dictionary mapping language code to list of batches
        manager: Translation manager
    """
    # Create a queue for all batches
    batch_queue = Queue()
    
    # Flag to signal workers to stop
    stop_flag = {'stop': False}
    
    # Set total batches for each file
    for lang_code, batches in language_batches.items():
        file_batch_counts = defaultdict(int)
        for batch in batches:
            file_batch_counts[batch.file_key] += 1
        
        for file_key, count in file_batch_counts.items():
            file_data = manager.file_data.get(file_key)
            if file_data:
                file_data.total_batches = count
    
    # Enqueue batches in round-robin fashion across languages
    max_batches = max(len(batches) for batches in language_batches.values())
    for i in range(max_batches):
        for lang_code in sorted(language_batches.keys()):
            batches = language_batches[lang_code]
            if i < len(batches):
                batch_queue.put((lang_code, batches[i]))
    
    # Create progress bars
    with Progress(
        SpinnerColumn(),
        TextColumn("[bold blue]{task.description}"),
        BarColumn(),
        TextColumn("[progress.percentage]{task.percentage:>3.0f}%"),
        TextColumn("({task.completed}/{task.total})"),
        TimeRemainingColumn(),
        console=console
    ) as progress:
        
        # Create a task for each language
        language_tasks = {}
        for lang_code, batches in language_batches.items():
            lang_name = LANGUAGE_NAMES.get(lang_code, lang_code)
            task_id = progress.add_task(f"[cyan]{lang_name:<20}[/cyan]", total=len(batches))
            language_tasks[lang_code] = task_id
        
        # Process batches concurrently
        def process_batch_from_queue():
            while not batch_queue.empty() and not stop_flag['stop']:
                try:
                    lang_code, batch = batch_queue.get_nowait()
                    if stop_flag['stop']:
                        break
                    task_id = language_tasks[lang_code]
                    process_translation_batch(batch, manager, progress, task_id)
                except:
                    break
        
        try:
            with ThreadPoolExecutor(max_workers=manager.max_workers) as executor:
                futures = [executor.submit(process_batch_from_queue) for _ in range(manager.max_workers)]
                for future in as_completed(futures):
                    try:
                        future.result()
                    except Exception as e:
                        if not stop_flag['stop']:
                            console.print(f"[red]Worker error: {e}[/red]")
        except KeyboardInterrupt:
            console.print("\n[yellow]Stopping translation workers...[/yellow]")
            stop_flag['stop'] = True
            raise


def process_path(input_path: str, source_lang_code: str, target_lang_codes: Optional[List[str]], force: bool = False):
    """
    Process a file or directory for translation.
    
    Args:
        input_path: Path to a file or directory to translate
        source_lang_code: Source language code
        target_lang_codes: List of target language codes (None for all)
        force: If True, retranslate all entries even if they already exist
    """
    path = Path(input_path)
    
    if not path.exists():
        console.print(f"[red]ERROR: Path does not exist: {input_path}[/red]")
        return
    
    # Get translations directory
    translations_dir = None
    for parent in path.parents:
        if parent.name == 'Translations':
            translations_dir = parent
            break
    
    if not translations_dir:
        console.print(f"[red]ERROR: Could not find Translations directory in path: {input_path}[/red]")
        return
    
    # Get target language codes
    if target_lang_codes is None:
        target_lang_codes = [d.name for d in translations_dir.iterdir() if d.is_dir() and d.name != source_lang_code]
    
    # Get source files
    if path.is_file():
        source_files = [str(path)]
    elif path.is_dir():
        source_files = [str(f) for f in path.rglob("*.txt")]
    else:
        console.print(f"[red]ERROR: Invalid path type[/red]")
        return
    
    if not source_files:
        console.print("[yellow]No files found to translate[/yellow]")
        return
    
    # Create translation manager
    manager = TranslationManager(max_workers=MAX_WORKERS)
    
    # Create batches
    console.print("\n[bold]Analyzing files and creating translation tasks...[/bold]")
    language_batches = create_translation_batches(
        source_files,
        source_lang_code,
        target_lang_codes,
        translations_dir,
        force
    )
    
    if not language_batches:
        console.print("[green]All translations are up to date![/green]")
        return
    
    # Initialize file data with existing translations
    for lang_code, batches in language_batches.items():
        for batch in batches:
            existing_data = read_localization_file(batch.target_file)
            manager.get_file_data(batch.file_key, batch.target_file, existing_data)
            file_data = manager.file_data[batch.file_key]
            # Count total batches for this file
            if file_data.total_batches == 0:
                file_batch_count = sum(1 for b in batches if b.file_key == batch.file_key)
                file_data.total_batches = file_batch_count
    
    # Display summary
    total_batches = sum(len(batches) for batches in language_batches.values())
    console.print(f"\n[bold]Translation Summary:[/bold]")
    console.print(f"  Source files: {len(source_files)}")
    console.print(f"  Target languages: {len(language_batches)}")
    console.print(f"  Total batches: {total_batches}")
    console.print(f"  Concurrent workers: {MAX_WORKERS}\n")
    
    # Run translation
    try:
        run_concurrent_translation(language_batches, manager)
        console.print("\n[bold green]✓ Translation completed![/bold green]\n")
    except KeyboardInterrupt:
        console.print("\n[yellow]Translation interrupted by user. Partial progress has been saved.[/yellow]\n")
        raise


def main():
    """Main entry point for the script."""
    parser = argparse.ArgumentParser(
        description='Translate game localization files using LLM API with concurrent processing',
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog='''
Examples:
  # Translate all files in a language folder (run from Scripts directory)
  python translateLLM.py en
  
  # Translate a single file (run from Scripts directory)
  python translateLLM.py en/Backgrounds-en.txt
  
  # Force retranslation of all entries (including existing ones)
  python translateLLM.py en --force
  
  # Translate to specific target language(s)
  python translateLLM.py zh-CN --target en
  python translateLLM.py zh-CN --target en,de,fr
        '''
    )
    
    parser.add_argument(
        'input_path',
        help='Path to a file or directory to translate (relative to SolastaUnfinishedBusiness/Translations/)'
    )
    
    parser.add_argument(
        '--force', '-f',
        action='store_true',
        help='Force retranslation of all entries, even if they already exist in target files'
    )
    
    parser.add_argument(
        '--target', '-t',
        type=str,
        help='Target language(s) to translate to (comma-separated). If not specified, translates to all languages.'
    )
    
    args = parser.parse_args()
    
    # Get the script directory and construct the base translations path
    script_dir = Path(__file__).parent
    translations_base = script_dir.parent / 'SolastaUnfinishedBusiness' / 'Translations'
    
    # Parse input path
    input_relative = Path(args.input_path)
    
    # Detect source language from path
    source_lang_code = None
    input_path = None
    
    # Check if first part of the path is a language code
    first_part = input_relative.parts[0] if input_relative.parts else None
    if first_part in LANGUAGE_NAMES:
        source_lang_code = first_part
        input_path = translations_base / input_relative
    else:
        # Try to detect language code from anywhere in the path
        for lang_code in LANGUAGE_NAMES.keys():
            if lang_code in input_relative.parts:
                source_lang_code = lang_code
                input_path = Path(args.input_path)
                if not input_path.is_absolute():
                    input_path = translations_base / input_relative
                break
    
    if not source_lang_code or input_path is None:
        console.print("[red]ERROR: Could not detect source language from path.[/red]")
        console.print("Path must start with or contain one of the supported language codes:")
        for code, name in LANGUAGE_NAMES.items():
            console.print(f"  {code}: {name}")
        console.print("\nExamples:")
        console.print("  python translateLLM.py en")
        console.print("  python translateLLM.py zh-CN/SubClasses/OathOfDemonHunter-zh-CN.txt")
        sys.exit(1)
    
    # Parse target languages
    target_lang_codes = None
    if args.target:
        target_lang_codes = [lang.strip() for lang in args.target.split(',')]
        # Validate target languages
        invalid_langs = [lang for lang in target_lang_codes if lang not in LANGUAGE_NAMES]
        if invalid_langs:
            console.print(f"[red]ERROR: Invalid target language(s): {', '.join(invalid_langs)}[/red]")
            console.print("Supported languages:")
            for code, name in LANGUAGE_NAMES.items():
                console.print(f"  {code}: {name}")
            sys.exit(1)
        # Remove source language from targets if present
        target_lang_codes = [lang for lang in target_lang_codes if lang != source_lang_code]
    
    # Display configuration
    console.print(Panel.fit(
        f"[bold]LLM Translation Script[/bold]\n\n"
        f"Model: {MODEL_ID}\n"
        f"Batch size: {BATCH_SIZE}\n"
        f"Max workers: {MAX_WORKERS}\n"
        f"Force mode: {'[red]ON[/red] (retranslate all)' if args.force else '[green]OFF[/green] (skip existing)'}\n"
        f"Source language: {LANGUAGE_NAMES.get(source_lang_code, source_lang_code)} ({source_lang_code})\n"
        f"Target languages: {', '.join(target_lang_codes) if target_lang_codes else 'All'}",
        title="Configuration",
        border_style="blue"
    ))
    
    try:
        process_path(str(input_path), source_lang_code, target_lang_codes, args.force)
    except KeyboardInterrupt:
        console.print("\n[yellow]Operation cancelled by user[/yellow]")
        sys.exit(130)  # Standard exit code for SIGINT
    except Exception as e:
        console.print(f"\n[red]ERROR: {e}[/red]")
        import traceback
        traceback.print_exc()
        sys.exit(1)


if __name__ == "__main__":
    main()
