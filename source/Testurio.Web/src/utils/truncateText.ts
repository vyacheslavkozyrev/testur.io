/**
 * Truncates a string to the given maximum length.
 *
 * @param text      - The source string to truncate.
 * @param maxLength - Maximum number of characters to keep before appending the ellipsis.
 * @returns The original string when its length is ≤ maxLength; otherwise the
 *          first maxLength characters (trailing whitespace trimmed) followed by `…`.
 */
export function truncateText(text: string, maxLength: number): string {
  if (text.length <= maxLength) {
    return text;
  }
  return text.slice(0, maxLength).trimEnd() + '\u2026';
}
