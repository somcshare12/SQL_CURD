interface NavigationRailProps {
  treeOpen: boolean;
  onToggleTree: () => void;
}

/**
 * The narrow navigation rail. It is intentionally separate from the object tree
 * so that, when embedded, it can be replaced by (or merged into) the parent
 * application's primary navigation. It exposes the tree open/close control and
 * a module indicator, and is fully keyboard accessible.
 */
export function NavigationRail({ treeOpen, onToggleTree }: NavigationRailProps) {
  return (
    <nav className="sdm-rail" aria-label="Module navigation">
      <button
        type="button"
        className="sdm-rail__button"
        aria-pressed={treeOpen}
        aria-label={treeOpen ? 'Collapse object tree' : 'Expand object tree'}
        title={treeOpen ? 'Collapse object tree' : 'Expand object tree'}
        onClick={onToggleTree}
      >
        ☰
      </button>
      <div className="sdm-rail__module is-selected" title="SQL Data Manager" aria-current="page">
        🗄
      </div>
    </nav>
  );
}
