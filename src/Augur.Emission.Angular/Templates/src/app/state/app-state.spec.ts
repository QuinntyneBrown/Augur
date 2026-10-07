import { TestBed } from '@angular/core/testing';
import { AppState } from './app-state';

describe('AppState', () => {
  it('toggles and closes the menu', () => {
    const state = TestBed.inject(AppState);
    expect(state.menuOpen()).toBe(false);

    state.toggleMenu();
    expect(state.menuOpen()).toBe(true);

    state.closeMenu();
    expect(state.menuOpen()).toBe(false);
  });
});
