import { patchState, signalStore, withMethods, withState } from '@ngrx/signals';

/** Application state shared across the app, held in an NgRx signal store. */
export const AppStore = signalStore(
  { providedIn: 'root' },
  withState({ menuOpen: false }),
  withMethods((store) => ({
    toggleMenu(): void {
      patchState(store, { menuOpen: !store.menuOpen() });
    },
    closeMenu(): void {
      patchState(store, { menuOpen: false });
    },
  })),
);
