import { Component, inject, OnInit, signal } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { HeaderComponent } from "./layout/header/header.component";
import { HttpClient } from '@angular/common/http';
import { Product } from './shared/product';
import { Pagination } from './shared/models/pagination';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, HeaderComponent],
  templateUrl: './app.component.html',
  styleUrls: ['./app.component.scss']
})
export class AppComponent implements OnInit {
  basUrl = 'https://localhost:5001/api/';
  private http = inject(HttpClient);
  title = signal('Skinet');
  products: Product[] = [];

  ngOnInit() {
    this.http.get<Pagination<Product>>(this.basUrl + 'products').subscribe({
      next: response => {
        this.products = response.data;
        console.log(response);
      },
      error: error => {
        console.error(error);
      },
      complete: () => {
        console.log('Request completed');
      }
    });
  }
}
