import { TestBed } from '@angular/core/testing';
import { AppStore } from './app-store';

describe('AppStore', () => {
  it('toggles and closes the menu', () => {
    const store = TestBed.inject(AppStore);
    expect(store.menuOpen()).toBe(false);

    store.toggleMenu();
    expect(store.menuOpen()).toBe(true);

    store.closeMenu();
    expect(store.menuOpen()).toBe(false);
  });
});
