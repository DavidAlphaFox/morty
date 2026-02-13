import * as signalR from '@microsoft/signalr';
import type { Story, Iteration } from '../types';

export interface SignalRCallbacks {
  onStoryUpdated?: (story: Story) => void;
  onIterationComplete?: (iteration: Iteration) => void;
  onConnected?: () => void;
  onDisconnected?: () => void;
  onReconnecting?: () => void;
}

export function createSignalRConnection(callbacks: SignalRCallbacks) {
  const connection = new signalR.HubConnectionBuilder()
    .withUrl('/morty-hub')
    .withAutomaticReconnect()
    .build();

  connection.on('OnStoryUpdated', (story: Story) => {
    callbacks.onStoryUpdated?.(story);
  });

  connection.on('OnIterationComplete', (iteration: Iteration) => {
    callbacks.onIterationComplete?.(iteration);
  });

  connection.onreconnecting(() => {
    callbacks.onReconnecting?.();
  });

  connection.onreconnected(() => {
    callbacks.onConnected?.();
  });

  connection.onclose(() => {
    callbacks.onDisconnected?.();
  });

  return {
    async start() {
      try {
        await connection.start();
        callbacks.onConnected?.();
      } catch (err) {
        console.error('SignalR connection error:', err);
        callbacks.onDisconnected?.();
      }
    },

    async stop() {
      await connection.stop();
    },

    async joinProject(projectId: number) {
      try {
        await connection.invoke('JoinProject', projectId);
      } catch {
        // JoinProject may not exist on the hub, that's ok
      }
    },

    async leaveProject(projectId: number) {
      try {
        await connection.invoke('LeaveProject', projectId);
      } catch {
        // LeaveProject may not exist on the hub, that's ok
      }
    },

    connection,
  };
}
