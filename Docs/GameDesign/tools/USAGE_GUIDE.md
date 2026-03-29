# Cross-Reference Validator - Usage Guide

## Quick Start

```bash
cd /sessions/great-sharp-dijkstra/mnt/AI_HANDOFF/Banngannka_Rebuild_Pack/tools
python3 validate_crossrefs.py
```

## What the Script Does

This validator checks the integrity of all cross-references in your markdown documentation:

1. **Scans all `.md` files** in the parent directory (54 files found)
2. **Extracts cross-references** - Identifies all references to other files
3. **Validates file existence** - Checks if referenced files actually exist
4. **Validates section references** - Ensures section numbers (§N, §N.N) exist in target files
5. **Finds orphan files** - Identifies files never referenced by any other file
6. **Detects deleted file references** - Lists references to files that no longer exist

## Understanding the Output

### Summary Statistics
```
Total files scanned:          54
Total cross-references:       748
Broken file references:       14
Broken section references:    28
Orphan files:                 4
Overall health score:         93.8%
```

### Health Score Interpretation

- **95-100%**: EXCELLENT - No issues detected
- **85-95%**: GOOD - Minor issues present (Current: 93.8%)
- **70-85%**: FAIR - Moderate issues need attention
- **50-70%**: POOR - Significant issues
- **0-50%**: CRITICAL - Major integrity problems

### Issues Explained

#### Broken File References
Shows files that reference other files that don't exist:
```
ANIMATION_SPEC.md:2
  References: ASSET_CARD_EFFECTS.md
  Missing file: ASSET_CARD_EFFECTS.md
```

#### Broken Section References
Shows references to sections that don't exist in the target file:
```
ACCESSIBILITY_SPEC.md:228
  References: GAME_DESIGN.md §5.1.1
  Section §5.1.1 not found in GAME_DESIGN.md
  Available sections: §0, §1, §10, §11, §12
```

#### Orphan Files
Files that are never referenced by any other file:
```
AUDIT_REPORT_v2.md
AUDIT_REPORT_v3.md
AUDIT_REPORT_v4.md
AUDIT_REPORT_v5.md
```

#### Deleted File References
Files referenced but no longer present:
```
ASSET_AUDIO.md: 3 reference(s)
ASSET_CARD_EFFECTS.md: 5 reference(s)
```

## Current Issues (Last Run)

### Immediate Action Items

1. **Fix Section Reference Mismatch** (28 broken references)
   - Many files reference subsections (§N.N) that don't exist
   - Example: References to `GAME_DESIGN.md §5.1.1` but file only has `§5`
   - Files to review:
     - GAME_DESIGN.md
     - BACKEND_DESIGN.md
     - NETWORK_SPEC.md
     - ERROR_HANDLING.md

2. **Handle Missing Files** (14 broken file references)
   - 6 files are referenced but don't exist:
     - ASSET_AUDIO.md
     - ASSET_CARD_EFFECTS.md
     - AUDIT_REPORT.md
     - QA_SPEC.md
     - SESSION_HANDOFF.md
     - _INDEX.md
   - Decision needed: Restore or update references?

3. **Review Orphan Files** (4 files)
   - AUDIT_REPORT_v2.md through v5.md are never referenced
   - Consider: Archive, consolidate, or delete?

## How to Fix Issues

### Fixing Section Reference Issues

1. Check the actual sections in the target file:
   ```bash
   grep "^##" GAME_DESIGN.md
   ```

2. Update references to match actual sections:
   - Change `§5.1.1` to `§5` if subsection doesn't exist
   - Or add the subsection to the file

### Fixing Missing File References

1. For README.md, update references to use correct filenames:
   ```
   Old: ASSET_AUDIO.md
   New: (Remove or update to available file)
   ```

2. For other files, either:
   - Restore the missing file from backup
   - Or update/remove the reference

### Handling Orphan Files

1. Check if they should be referenced:
   ```bash
   grep -r "AUDIT_REPORT_v2" .
   # If no results, file is truly orphaned
   ```

2. Options:
   - Move to archive directory
   - Add references if they should be used
   - Delete if no longer needed

## Advanced Usage

### Running with Custom Parameters (Future Enhancement)

The script could be extended with:
- `--strict` mode (fail on any broken references)
- `--fix-auto` mode (auto-correct simple issues)
- `--json` output format (for processing)

Currently not implemented but possible future additions.

## Technical Details

### Reference Pattern Matching
The validator looks for:
- `FILENAME.md` - Simple file reference
- `FILENAME.md §5` - Reference to section 5
- `FILENAME.md §5.1` - Reference to subsection 5.1
- `` `FILENAME.md` `` - Backtick-quoted references

### Section Detection
Looks for markdown headers matching pattern:
- `## 1. Section Title` → Creates section `1`
- `### 2.1. Subsection Title` → Creates section `2.1`

## Troubleshooting

### Script doesn't run
```bash
# Check Python version
python3 --version  # Should be 3.6+

# Make sure you're in the tools directory
cd /sessions/great-sharp-dijkstra/mnt/AI_HANDOFF/Banngannka_Rebuild_Pack/tools

# Check script is executable
ls -l validate_crossrefs.py  # Should show 'x' permission
```

### No markdown files found
- Ensure you're running from the tools directory
- Script automatically looks in parent directory
- Check files exist: `ls *.md` in parent directory

### Encoding issues
- Script handles UTF-8 (supports Japanese text)
- If issues occur, ensure files are saved as UTF-8

## Contact & Support

For issues or improvements, check:
- SCRIPT_SUMMARY.txt - Technical implementation details
- VALIDATION_RESULTS.txt - Current validation status
- README.md - Quick reference guide

