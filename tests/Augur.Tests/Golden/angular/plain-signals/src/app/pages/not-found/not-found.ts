import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';

@Component({
  selector: 'app-not-found',
  imports: [RouterLink],
  template: `
    <h1 tabindex="-1">Page not found</h1>
    <p>There is no page at this address.</p>
    <p><a class="button" routerLink="/">Go to the home page</a></p>
  `,
})
export class NotFound {}
