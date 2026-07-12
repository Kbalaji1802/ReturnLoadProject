import { Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';

/**
 * Root shell. Deliberately thin: it only hosts the router outlet. Chrome
 * (toolbar, navigation) lives in the layout components so features stay
 * decoupled from the frame.
 */
@Component({
  selector: 'app-root',
  imports: [RouterOutlet],
  template: '<router-outlet />',
})
export class App {}
