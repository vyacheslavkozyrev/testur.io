import { truncateText } from './truncateText';

describe('truncateText', () => {
  it('returns the original string unchanged when length equals maxLength', () => {
    const text = 'A'.repeat(120);
    expect(truncateText(text, 120)).toBe(text);
  });

  it('returns the original string unchanged when length is below maxLength', () => {
    const text = 'Short string.';
    expect(truncateText(text, 120)).toBe(text);
  });

  it('truncates text that exceeds maxLength and appends an ellipsis', () => {
    const text = 'A'.repeat(150);
    const result = truncateText(text, 120);
    expect(result).toBe('A'.repeat(120) + '\u2026');
  });

  it('trims trailing whitespace before appending the ellipsis', () => {
    const text = 'Hello   ' + 'A'.repeat(115);
    const result = truncateText(text, 10);
    // First 10 chars: "Hello     " — trimEnd → "Hello" + ellipsis
    expect(result).toBe('Hello\u2026');
  });

  it('handles an empty string without throwing', () => {
    expect(truncateText('', 120)).toBe('');
  });

  it('returns the ellipsis only when maxLength is 0 and text is non-empty', () => {
    expect(truncateText('Hello', 0)).toBe('\u2026');
  });
});
