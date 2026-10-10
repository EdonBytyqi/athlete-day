import { Injectable, signal } from '@angular/core';
import { HubConnection, HubConnectionBuilder, HubConnectionState, LogLevel } from '@microsoft/signalr';

export type LiveState = 'connecting' | 'live' | 'offline';

/**
 * Listens for "data changed" pushes from the API. The push carries no athlete data; screens
 * simply reload through the API with the current role. Bumping `version` triggers that reload.
 */
@Injectable({ providedIn: 'root' })
export class RealtimeService {
  readonly version = signal(0);
  readonly state = signal<LiveState>('connecting');
  readonly lastReason = signal<string | null>(null);

  private connection?: HubConnection;

  start(): void {
    if (this.connection) return;

    this.connection = new HubConnectionBuilder()
      .withUrl('/hubs/dashboard')
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning)
      .build();

    this.connection.on('dataChanged', (msg: { reason: string }) => {
      this.lastReason.set(msg.reason);
      this.version.update((v) => v + 1);
    });
    this.connection.onreconnecting(() => this.state.set('connecting'));
    this.connection.onreconnected(() => this.state.set('live'));
    this.connection.onclose(() => this.state.set('offline'));

    this.connection
      .start()
      .then(() => this.state.set(this.connection?.state === HubConnectionState.Connected ? 'live' : 'offline'))
      .catch(() => this.state.set('offline'));
  }
}
