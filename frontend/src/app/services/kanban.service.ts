import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { KanbanCard } from '../models/kanban-card';
import { environment } from '../config';

export interface CreateCardPayload {
  title: string;
  description?: string | null;
  status: string;
}

export interface UpdateCardPayload extends CreateCardPayload {
  position: number;
}

export interface ReorderItemPayload {
  id: number;
  status: string;
  position: number;
}

@Injectable({ providedIn: 'root' })
export class KanbanService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/cards`;
  private readonly exportUrl = `${environment.apiUrl}/export`;

  getCards(): Observable<KanbanCard[]> {
    return this.http.get<KanbanCard[]>(this.baseUrl);
  }

  createCard(payload: CreateCardPayload): Observable<KanbanCard> {
    return this.http.post<KanbanCard>(this.baseUrl, payload);
  }

  updateCard(id: number, payload: UpdateCardPayload): Observable<KanbanCard> {
    return this.http.put<KanbanCard>(`${this.baseUrl}/${id}`, payload);
  }

  deleteCard(id: number) {
    return this.http.delete(`${this.baseUrl}/${id}`);
  }

  reorderCards(items: ReorderItemPayload[]) {
    return this.http.post<void>(`${this.baseUrl}/reorder`, items);
  }

  downloadPdf() {
    return this.http.get(`${this.exportUrl}/pdf`, { responseType: 'blob' });
  }

  downloadExcel() {
    return this.http.get(`${this.exportUrl}/excel`, { responseType: 'blob' });
  }
}
