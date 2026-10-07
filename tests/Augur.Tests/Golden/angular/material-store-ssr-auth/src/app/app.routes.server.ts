import { RenderMode, ServerRoute } from '@angular/ssr';

/** Public pages are prerendered at build time; pages that depend on the signed-in user render in the browser. */
export const serverRoutes: ServerRoute[] = [
  { path: 'notes', renderMode: RenderMode.Client },
  { path: 'sign-in', renderMode: RenderMode.Client },
  { path: 'sign-out', renderMode: RenderMode.Client },
  { path: '**', renderMode: RenderMode.Prerender },
];
