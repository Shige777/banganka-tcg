#!/usr/bin/env python3
"""
Cross-reference validator for markdown files in Banngannka_Rebuild_Pack.

Scans all .md files and validates:
- File references exist
- Section references (§N, §N.N) point to valid sections
- Identifies orphan files
- Detects references to deleted files
"""

import os
import re
import sys
from pathlib import Path
from collections import defaultdict

class CrossRefValidator:
    def __init__(self, base_dir):
        self.base_dir = Path(base_dir).resolve()
        self.files = {}
        self.sections = {}
        self.references = defaultdict(list)  # {source_file: [(ref, line_num, match_text)]}
        self.broken_files = []
        self.broken_sections = []
        self.orphans = []

    def scan_files(self):
        """Scan all .md files in the directory."""
        md_files = list(self.base_dir.glob("*.md"))

        if not md_files:
            print("ERROR: No markdown files found in the directory.")
            return False

        for md_file in md_files:
            filename = md_file.name
            self.files[filename] = md_file

        return len(md_files)

    def extract_sections(self):
        """Extract section numbers from markdown files (## N. and ### N.N patterns)."""
        # Match section numbers with or without trailing period: "## 5." or "### 5.1.1 Title"
        section_pattern = re.compile(r'^(#{2,4})\s+(\d+(?:\.\d+)*)[\s.]')

        for filename, filepath in self.files.items():
            sections = set()
            try:
                with open(filepath, 'r', encoding='utf-8') as f:
                    for line_num, line in enumerate(f, 1):
                        match = section_pattern.match(line)
                        if match:
                            section_num = match.group(2)
                            sections.add(section_num)
                self.sections[filename] = sections
            except Exception as e:
                print(f"Warning: Could not read {filename}: {e}")
                self.sections[filename] = set()

    def extract_references(self):
        """Extract all markdown cross-references from files."""
        # Pattern to match: FILENAME.md, FILENAME.md §N, FILENAME.md §N.N, etc.
        ref_pattern = re.compile(r'`?([A-Z_][A-Z0-9_]*\.md)(?:\s+§([\d.]+))?`?')

        for filename, filepath in self.files.items():
            try:
                with open(filepath, 'r', encoding='utf-8') as f:
                    for line_num, line in enumerate(f, 1):
                        matches = ref_pattern.finditer(line)
                        for match in matches:
                            ref_file = match.group(1)
                            ref_section = match.group(2)
                            match_text = match.group(0)

                            # Skip references to itself
                            if ref_file != filename:
                                self.references[filename].append({
                                    'file': ref_file,
                                    'section': ref_section,
                                    'line': line_num,
                                    'text': match_text.strip('`'),
                                    'full_line': line.strip()
                                })
            except Exception as e:
                print(f"Warning: Could not read {filename}: {e}")

    def validate_references(self):
        """Validate all extracted references."""
        for source_file, refs in self.references.items():
            for ref in refs:
                ref_file = ref['file']
                ref_section = ref['section']

                # Check if file exists
                if ref_file not in self.files:
                    self.broken_files.append({
                        'source': source_file,
                        'target': ref_file,
                        'section': ref_section,
                        'line': ref['line'],
                        'text': ref['text']
                    })
                elif ref_section:
                    # Validate section exists
                    if ref_file in self.sections:
                        available_sections = self.sections[ref_file]
                        if ref_section not in available_sections:
                            self.broken_sections.append({
                                'source': source_file,
                                'target': ref_file,
                                'section': ref_section,
                                'line': ref['line'],
                                'text': ref['text'],
                                'available': sorted(available_sections)
                            })

    def find_orphans(self):
        """Identify files that are never referenced by any other file."""
        referenced_files = set()
        for refs in self.references.values():
            for ref in refs:
                referenced_files.add(ref['file'])

        for filename in self.files.keys():
            if filename not in referenced_files:
                self.orphans.append(filename)

    def calculate_health_score(self):
        """Calculate overall health score (0-100)."""
        if not self.files:
            return 0

        total_refs = sum(len(refs) for refs in self.references.values())
        if total_refs == 0:
            broken_score = 0
        else:
            broken_refs = len(self.broken_files) + len(self.broken_sections)
            broken_score = (broken_refs / total_refs) * 100

        orphan_score = (len(self.orphans) / len(self.files)) * 100 if self.files else 0

        # Health = 100 - (broken% + orphan%)
        health = 100 - (broken_score * 0.7 + orphan_score * 0.3)
        return max(0, min(100, health))

    def generate_report(self):
        """Generate validation report."""
        print("\n" + "=" * 70)
        print("CROSS-REFERENCE VALIDATION REPORT")
        print("=" * 70)
        print(f"Directory: {self.base_dir}")
        print()

        # Summary Statistics
        print("SUMMARY STATISTICS")
        print("-" * 70)
        print(f"Total files scanned:          {len(self.files)}")
        print(f"Total cross-references:       {sum(len(refs) for refs in self.references.values())}")
        print(f"Broken file references:       {len(self.broken_files)}")
        print(f"Broken section references:    {len(self.broken_sections)}")
        print(f"Orphan files:                 {len(self.orphans)}")
        print(f"Overall health score:         {self.calculate_health_score():.1f}%")
        print()

        # Broken File References
        if self.broken_files:
            print("BROKEN FILE REFERENCES")
            print("-" * 70)
            for item in sorted(self.broken_files, key=lambda x: (x['source'], x['line'])):
                print(f"  {item['source']}:{item['line']}")
                print(f"    References: {item['text']}")
                print(f"    Missing file: {item['target']}")
                print()

        # Broken Section References
        if self.broken_sections:
            print("BROKEN SECTION REFERENCES")
            print("-" * 70)
            for item in sorted(self.broken_sections, key=lambda x: (x['source'], x['line'])):
                print(f"  {item['source']}:{item['line']}")
                print(f"    References: {item['text']}")
                print(f"    Section §{item['section']} not found in {item['target']}")
                if item['available']:
                    print(f"    Available sections: {', '.join('§' + s for s in item['available'][:5])}")
                    if len(item['available']) > 5:
                        print(f"                       ... and {len(item['available']) - 5} more")
                print()

        # Orphan Files
        if self.orphans:
            print("ORPHAN FILES (not referenced by any other file)")
            print("-" * 70)
            for filename in sorted(self.orphans):
                print(f"  - {filename}")
            print()

        # Known Deleted Files Check
        print("REFERENCES TO POTENTIALLY DELETED FILES")
        print("-" * 70)
        deleted_candidates = set(item['target'] for item in self.broken_files)
        if deleted_candidates:
            for filename in sorted(deleted_candidates):
                count = sum(1 for item in self.broken_files if item['target'] == filename)
                print(f"  {filename}: {count} reference(s)")
            print()
        else:
            print("  None detected")
            print()

        # Health Verdict
        print("HEALTH VERDICT")
        print("-" * 70)
        health = self.calculate_health_score()
        if health >= 95:
            verdict = "EXCELLENT - No issues detected"
        elif health >= 85:
            verdict = "GOOD - Minor issues present"
        elif health >= 70:
            verdict = "FAIR - Moderate issues need attention"
        elif health >= 50:
            verdict = "POOR - Significant issues"
        else:
            verdict = "CRITICAL - Major integrity problems"

        print(f"Score: {health:.1f}% - {verdict}")
        print("=" * 70)
        print()

    def run(self):
        """Run the full validation."""
        file_count = self.scan_files()
        if not file_count:
            return False

        print(f"Scanning {file_count} markdown files...")
        self.extract_sections()
        print(f"Extracted sections from {len(self.sections)} files")

        print("Extracting cross-references...")
        self.extract_references()
        total_refs = sum(len(refs) for refs in self.references.values())
        print(f"Found {total_refs} cross-references")

        print("Validating references...")
        self.validate_references()

        print("Identifying orphan files...")
        self.find_orphans()

        self.generate_report()
        return True


def main():
    # Get the base directory (parent of tools directory)
    script_dir = Path(__file__).resolve().parent
    base_dir = script_dir.parent

    validator = CrossRefValidator(base_dir)
    success = validator.run()

    sys.exit(0 if success else 1)


if __name__ == "__main__":
    main()
