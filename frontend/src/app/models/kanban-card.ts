export interface KanbanCard {
  id: number;
  title: string;
  description?: string | null;
  status: string;
  position: number;
  createdAt: string;
  updatedAt: string;
}
