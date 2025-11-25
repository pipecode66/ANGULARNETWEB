import { CommonModule } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { CdkDrag, CdkDragDrop, CdkDropList, CdkDropListGroup, moveItemInArray, transferArrayItem } from '@angular/cdk/drag-drop';
import { KanbanService, ReorderItemPayload } from '../../services/kanban.service';
import { KanbanCard } from '../../models/kanban-card';

interface Column {
  key: string;
  title: string;
  accent: string;
}

@Component({
  selector: 'app-board',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, CdkDropListGroup, CdkDropList, CdkDrag],
  templateUrl: './board.component.html',
  styleUrl: './board.component.scss'
})
export class BoardComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly kanbanService = inject(KanbanService);

  readonly columns: Column[] = [
    { key: 'todo', title: 'Por hacer', accent: '#e2e8f0' },
    { key: 'doing', title: 'En progreso', accent: '#c7d2fe' },
    { key: 'done', title: 'Listo', accent: '#bbf7d0' }
  ];

  readonly board = signal<Record<string, KanbanCard[]>>({
    todo: [],
    doing: [],
    done: []
  });

  readonly loading = signal(false);
  readonly saving = signal(false);
  readonly exportLoading = signal(false);
  readonly error = signal<string | null>(null);
  readonly editingId = signal<number | null>(null);

  readonly stats = computed(() => {
    const data = this.board();
    const total = Object.values(data).reduce((acc, list) => acc + list.length, 0);
    return {
      total,
      todo: data['todo']?.length ?? 0,
      doing: data['doing']?.length ?? 0,
      done: data['done']?.length ?? 0
    };
  });

  readonly cardForm = this.fb.nonNullable.group({
    title: ['', [Validators.required, Validators.minLength(3)]],
    description: [''],
    status: ['todo', Validators.required]
  });

  ngOnInit(): void {
    this.loadBoard();
  }

  loadBoard() {
    this.loading.set(true);
    this.error.set(null);

    this.kanbanService.getCards().subscribe({
      next: (cards) => {
        const grouped: Record<string, KanbanCard[]> = { todo: [], doing: [], done: [] };
        for (const card of cards) {
          const normalized = this.normalize(card.status);
          if (!grouped[normalized]) {
            grouped[normalized] = [];
          }
          grouped[normalized].push({ ...card, status: normalized });
        }

        for (const key of Object.keys(grouped)) {
          grouped[key] = grouped[key].sort((a, b) => a.position - b.position);
        }

        this.board.set(grouped);
        this.loading.set(false);
      },
      error: () => {
        this.error.set('No se pudo cargar el tablero.');
        this.loading.set(false);
      }
    });
  }

  saveCard() {
    if (this.cardForm.invalid) {
      this.cardForm.markAllAsTouched();
      return;
    }

    this.saving.set(true);
    const payload = this.cardForm.getRawValue();
    const editingId = this.editingId();

    if (editingId) {
      const targetColumn = this.board()[payload.status] ?? [];
      const position = targetColumn.findIndex((c) => c.id === editingId);
      const request = { ...payload, position: Math.max(position, 0) };
      this.kanbanService.updateCard(editingId, request).subscribe({
        next: () => {
          this.saving.set(false);
          this.resetForm();
          this.loadBoard();
        },
        error: () => {
          this.error.set('No se pudo actualizar la tarjeta.');
          this.saving.set(false);
        }
      });
    } else {
      this.kanbanService.createCard(payload).subscribe({
        next: () => {
          this.saving.set(false);
          this.resetForm();
          this.loadBoard();
        },
        error: () => {
          this.error.set('No se pudo crear la tarjeta.');
          this.saving.set(false);
        }
      });
    }
  }

  editCard(card: KanbanCard) {
    this.editingId.set(card.id);
    this.cardForm.patchValue({
      title: card.title,
      description: card.description ?? '',
      status: card.status
    });
  }

  deleteCard(card: KanbanCard) {
    if (!confirm(`¿Eliminar "${card.title}"?`)) {
      return;
    }
    this.kanbanService.deleteCard(card.id).subscribe({
      next: () => this.loadBoard(),
      error: () => this.error.set('No se pudo eliminar la tarjeta.')
    });
  }

  drop(event: CdkDragDrop<KanbanCard[]>, targetStatus: string) {
    const board = this.board();
    const prevStatus = this.findStatusForList(board, event.previousContainer.data);
    if (!prevStatus) {
      return;
    }

    if (event.previousContainer === event.container) {
      moveItemInArray(event.container.data, event.previousIndex, event.currentIndex);
    } else {
      transferArrayItem(event.previousContainer.data, event.container.data, event.previousIndex, event.currentIndex);
      event.container.data[event.currentIndex].status = targetStatus;
    }

    this.board.set({ ...board });
    const statusesToUpdate = new Set<string>([targetStatus, prevStatus]);
    this.persistReorder(statusesToUpdate);
  }

  downloadPdf() {
    this.exportLoading.set(true);
    this.kanbanService.downloadPdf().subscribe({
      next: (blob) => {
        this.exportLoading.set(false);
        this.saveBlob(blob, 'kanban.pdf');
      },
      error: () => {
        this.error.set('No se pudo generar el PDF.');
        this.exportLoading.set(false);
      }
    });
  }

  downloadExcel() {
    this.exportLoading.set(true);
    this.kanbanService.downloadExcel().subscribe({
      next: (blob) => {
        this.exportLoading.set(false);
        this.saveBlob(blob, 'kanban.xlsx');
      },
      error: () => {
        this.error.set('No se pudo generar el Excel.');
        this.exportLoading.set(false);
      }
    });
  }

  cancelEdit() {
    this.resetForm();
  }

  private persistReorder(statuses: Set<string>) {
    const payload: ReorderItemPayload[] = [];
    const board = this.board();

    for (const status of statuses) {
      const list = board[status];
      if (!list) continue;
      list.forEach((card, index) => {
        payload.push({ id: card.id, status, position: index });
      });
    }

    this.kanbanService.reorderCards(payload).subscribe({
      next: () => this.loadBoard(),
      error: () => this.error.set('No se pudo reordenar. Recarga el tablero.')
    });
  }

  private saveBlob(blob: Blob, filename: string) {
    const url = window.URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = filename;
    a.click();
    window.URL.revokeObjectURL(url);
  }

  private resetForm() {
    this.editingId.set(null);
    this.cardForm.reset({ title: '', description: '', status: 'todo' });
  }

  private findStatusForList(board: Record<string, KanbanCard[]>, list: KanbanCard[]): string | null {
    const entry = Object.entries(board).find(([_, cards]) => cards === list);
    return entry ? entry[0] : null;
  }

  private normalize(status: string) {
    return status?.toLowerCase() ?? 'todo';
  }
}
