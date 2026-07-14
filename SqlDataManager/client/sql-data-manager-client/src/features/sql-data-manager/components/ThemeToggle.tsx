import { useTheme } from '../hooks/useTheme';

/**
 * Light/dark theme toggle used only in standalone mode. In embedded mode the
 * parent application's theme is inherited and this control is not rendered.
 */
export function ThemeToggle() {
  const { resolved, toggle } = useTheme();
  return (
    <button
      type="button"
      className="sdm-btn sdm-theme-toggle"
      onClick={toggle}
      aria-label={`Switch to ${resolved === 'dark' ? 'light' : 'dark'} theme`}
      title={`Switch to ${resolved === 'dark' ? 'light' : 'dark'} theme`}
    >
      {resolved === 'dark' ? '☀' : '🌙'}
    </button>
  );
}
