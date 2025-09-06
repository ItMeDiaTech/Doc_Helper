# Test Files for Bulk Editor Hyperlink Processing

This directory contains test files created to validate the Bulk Editor's hyperlink processing capabilities.

## Files Created

### 1. TestData_HyperlinkDictionary.csv
- **Purpose**: Fake dictionary/database containing 50+ rows of hyperlink metadata
- **Columns**: Title, Content_ID, Document_ID, Status
- **Usage**: Used by the Bulk Editor to look up correct titles and content IDs for hyperlinks
- **Content_ID Pattern**: `TSRC-XXX-123456` or `CMS-XXX-123456` format (e.g., TSRC-SEC-012345)
- **Document_ID Pattern**: Random alphanumeric strings (e.g., doc001a2b3c4d5e, abc123def456)
- **Key Point**: Each record has BOTH a Content_ID AND a separate Document_ID - they are distinct values
- **Status Values**: "Released", "Expired", "Draft" etc.

### 2. TestDocument_HyperlinkErrors.html
- **Purpose**: Test document containing 12 different types of hyperlink errors plus 3 working links
- **To Use**: Open in Microsoft Word and save as .docx format for testing

## Error Types Included

### Hyperlink Errors to Fix (15 total):

1. **Missing Content IDs (3 cases)**:
   - Links that should have `(123456)` appended to display text
   - Content IDs: TSRC-SEC-012345, TSRC-HR-023456, TSRC-IT-034567

2. **Invisible/Empty Hyperlinks (3 cases)**:
   - Links with empty or whitespace-only display text
   - Should be completely removed from document
   - Content IDs: TSRC-FIN-045678, TSRC-LEG-056789, TSRC-SAF-067890

3. **Double Whitespace Issues (2 cases)**:
   - Paragraphs and links with multiple consecutive spaces
   - Should be reduced to single spaces

4. **Outdated Titles (3 cases)**:
   - Links with incorrect titles that should be updated from dictionary
   - Content IDs: TSRC-PM-089012, TSRC-CS-090123, TSRC-SC-001234

5. **URLs with docid= Parameter (3 cases)**:
   - Links using `?docid=randomstring` format where Document_ID can be looked up in dictionary
   - Document_IDs in URLs: abc123def456, xyz789ghi012, mno345pqr678
   - Dictionary lookup finds: Content_IDs TSRC-VEN-667788, TSRC-CHG-778899, TSRC-DOC-889900
   - Should extract Document_ID from URL, lookup in dictionary to get Content_ID and Title

6. **Complex Cases (1 case)**:
   - Link with anchor fragment and missing content ID
   - Content ID: TSRC-MKT-223344#section1

### Working Links (Should NOT be Changed - 3 total):

1. **External Links (2 cases)**:
   - Links to microsoft.com and github.com
   - No TSRC document ID pattern, should be ignored

2. **Already Correct Link (1 case)**:
   - Link that already has correct content ID appended
   - Document ID: TSRC-DEV-112233

## Expected Processing Results

When the test document is processed by the Bulk Editor with all options enabled:

- **Links Fixed**: 15 hyperlinks should be modified  
- **Links Unchanged**: 3 hyperlinks should remain as-is
- **Content IDs Added**: 7 links should get content IDs appended (including docid= cases)
- **Invisible Links Removed**: 3 empty hyperlinks should be deleted
- **Titles Updated**: 4 links should get updated titles from dictionary
- **Whitespace Fixed**: Multiple double-space issues should be resolved

## Dictionary Data Details

The CSV file contains hyperlink data that matches the document ID patterns used in the test document. Key entries include:

- TSRC-SEC-012345 → "Corporate Security Guidelines v2.3" (123456)
- TSRC-HR-023456 → "Employee Handbook 2024 Edition" (234567)  
- TSRC-IT-034567 → "Network Infrastructure Policy" (345678)
- TSRC-PM-089012 → "Project Management Methodology" (890123)
- TSRC-CS-090123 → "Customer Service Best Practices" (901234)

## Usage Instructions

1. Open `TestDocument_HyperlinkErrors.html` in Microsoft Word
2. Save it as `TestDocument_HyperlinkErrors.docx`  
3. Load the CSV file into the Bulk Editor's dictionary/database
4. Process the DOCX file with the Bulk Editor
5. Verify that all 12 errors are detected and fixed while 3 working links remain unchanged

## Testing Different Processing Options

Use the test files to verify individual processing features:

- **Fix Source Hyperlinks**: Should process all TSRC-pattern links
- **Append Content ID**: Should add (123456) format IDs to display text  
- **Check/Fix Titles**: Should update outdated titles from dictionary
- **Fix Internal Hyperlinks**: Should validate anchor links
- **Fix Double Spaces**: Should clean up whitespace issues
- **Remove Invisible Links**: Should delete empty hyperlinks