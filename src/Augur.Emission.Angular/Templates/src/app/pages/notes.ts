import { Component } from '@angular/core';

/** A page only signed-in users can open. Replace it with your own protected feature. */
@Component({
  selector: 'app-notes',
  template: `
    <h1 tabindex="-1">Notes</h1>
    <p>Only signed-in users can see this page.</p>
  `,
})
export class Notes {}
