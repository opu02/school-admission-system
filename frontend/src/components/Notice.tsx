export interface NoticeMessage {
  id: number;
  kind: 'success' | 'error' | 'info';
  text: string;
}

interface Props {
  notices: NoticeMessage[];
  onDismiss: (id: number) => void;
}

/** Stack of short messages in the corner (success / error / info). */
export function Notices({ notices, onDismiss }: Props) {
  return (
    <div className="notices" aria-live="assertive">
      {notices.map((n) => (
        <div key={n.id} className={`notice notice--${n.kind}`} role={n.kind === 'error' ? 'alert' : 'status'}>
          <span>{n.text}</span>
          <button type="button" className="notice__close" aria-label="Dismiss" onClick={() => onDismiss(n.id)}>
            ×
          </button>
        </div>
      ))}
    </div>
  );
}
