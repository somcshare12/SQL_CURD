import type { DatabaseObject } from '../models';
import { makeRoutes } from '../utilities/routing';

/**
 * A navigation node the parent application can render in its own global
 * navigation when embedding the module. This lets the module contribute entries
 * (for example a "Data Manager" section) without owning the parent's nav shell.
 */
export interface SqlDataManagerNavigationNode {
  id: string;
  label: string;
  path: string;
  icon: string;
  children?: SqlDataManagerNavigationNode[];
}

/**
 * Builds navigation nodes from the visible objects. Exposed publicly so a
 * parent can merge these into its own navigation tree.
 */
export function createSqlDataManagerNavigationNodes(
  objects: DatabaseObject[],
  basePath = '/sql-data-manager',
): SqlDataManagerNavigationNode {
  const routes = makeRoutes(basePath);

  return {
    id: 'sql-data-manager',
    label: 'SQL Data Manager',
    path: routes.root(),
    icon: 'database',
    children: objects.map((o) => ({
      id: o.fullName,
      label: o.name,
      path: routes.object(o),
      icon: o.objectType === 'View' ? 'view' : 'table',
    })),
  };
}
