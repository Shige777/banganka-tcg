# Cross-Reference Validator

A Python script that validates all cross-references between markdown files in the Banngannka_Rebuild_Pack documentation.

## Usage

```bash
cd /sessions/great-sharp-dijkstra/mnt/AI_HANDOFF/Banngannka_Rebuild_Pack/tools
python3 validate_crossrefs.py
```

## Features

The validator checks:

1. **File References** - Ensures all referenced files (e.g., `FILENAME.md`) actually exist
2. **Section References** - Verifies section numbers (e.g., `FILENAME.md §N` or `FILENAME.md §N.N`) exist in target files
3. **Orphan Files** - Identifies markdown files that are never referenced by any other file
4. **Deleted File References** - Reports references to files that no longer exist

## Output

The script generates a comprehensive report including:

- **Summary Statistics**: Total files, references, and broken items
- **Broken File References**: Shows which files reference missing files
- **Broken Section References**: Shows incorrect section number references with available sections listed
- **Orphan Files**: Files not referenced anywhere
- **Deleted Files**: Files referenced but no longer present
- **Health Score**: Overall quality metric (0-100%)

## Example Output

```
Total files scanned:          54
Total cross-references:       748
Broken file references:       14
Broken section references:    28
Orphan files:                 4
Overall health score:         93.8%
```

## Requirements

- Python 3.6+
- No external dependencies (uses only standard library)

## Script Location

- **Script**: `/sessions/great-sharp-dijkstra/mnt/AI_HANDOFF/Banngannka_Rebuild_Pack/tools/validate_crossrefs.py`
- **Results**: See output or check `VALIDATION_RESULTS.txt`
