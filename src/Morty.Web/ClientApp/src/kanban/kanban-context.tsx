import { createSignal, createMemo, createEffect, onCleanup, type Accessor, type ParentComponent } from 'solid-js';
import { createSafeContext } from '@ui/shared';
import type { Story, StoryStatus, Priority, KanbanColumn } from '../types';
import { STORY_STATUSES, COLUMN_CONFIG } from '../types';
import { fetchProjectStories, createStory as apiCreateStory, updateStory as apiUpdateStory } from '../api/client';
import { createSignalRConnection, type SignalRCallbacks } from '../api/signalr';

export interface KanbanContextValue {
  columns: Accessor<KanbanColumn[]>;
  stories: Accessor<Story[]>;
  selectedStory: Accessor<Story | null>;
  projectId: Accessor<number | null>;
  connectionStatus: Accessor<'connected' | 'disconnected' | 'connecting'>;

  setProject: (projectId: number) => void;
  addStory: (status: StoryStatus, title: string) => Promise<void>;
  moveStory: (storyId: number, toStatus: StoryStatus) => Promise<void>;
  updateStoryPriority: (storyId: number, priority: Priority) => Promise<void>;
  openStoryDetail: (story: Story) => void;
  closeStoryDetail: () => void;
  refreshStories: () => Promise<void>;
}

const [KanbanContext, useKanbanContext] = createSafeContext<KanbanContextValue>(
  'Kanban components must be used within KanbanProvider'
);

export { useKanbanContext };

export const KanbanProvider: ParentComponent = (props) => {
  const [stories, setStories] = createSignal<Story[]>([]);
  const [selectedStory, setSelectedStory] = createSignal<Story | null>(null);
  const [projectId, setProjectId] = createSignal<number | null>(null);
  const [connectionStatus, setConnectionStatus] = createSignal<'connected' | 'disconnected' | 'connecting'>('connecting');

  const columns = createMemo<KanbanColumn[]>(() => {
    const allStories = stories();
    return STORY_STATUSES.map((status) => ({
      id: status,
      title: COLUMN_CONFIG[status].title,
      color: COLUMN_CONFIG[status].color,
      stories: allStories.filter((s) => s.status === status),
    }));
  });

  // SignalR
  const signalrCallbacks: SignalRCallbacks = {
    onStoryUpdated: (story) => {
      setStories((prev) => {
        const idx = prev.findIndex((s) => s.id === story.id);
        if (idx >= 0) {
          const next = [...prev];
          next[idx] = story;
          return next;
        }
        // New story for this project
        if (story.projectId === projectId()) {
          return [...prev, story];
        }
        return prev;
      });
      // Update selected story if it's the same one
      const sel = selectedStory();
      if (sel && sel.id === story.id) {
        setSelectedStory(story);
      }
    },
    onIterationComplete: () => {
      // Reload stories to get latest state
      const pid = projectId();
      if (pid) {
        fetchProjectStories(pid).then(setStories).catch(console.error);
      }
    },
    onConnected: () => setConnectionStatus('connected'),
    onDisconnected: () => setConnectionStatus('disconnected'),
    onReconnecting: () => setConnectionStatus('connecting'),
  };

  const signalr = createSignalRConnection(signalrCallbacks);

  // Start connection
  setConnectionStatus('connecting');
  signalr.start();

  onCleanup(() => {
    signalr.stop();
  });

  const refreshStories = async () => {
    const pid = projectId();
    if (!pid) return;
    try {
      const data = await fetchProjectStories(pid);
      setStories(data);
    } catch (err) {
      console.error('Failed to fetch stories:', err);
    }
  };

  const setProject = (id: number) => {
    const prevId = projectId();
    if (prevId) {
      signalr.leaveProject(prevId);
    }
    setProjectId(id);
    setSelectedStory(null);
    signalr.joinProject(id);
    fetchProjectStories(id).then(setStories).catch(console.error);
  };

  const addStory = async (status: StoryStatus, title: string) => {
    const pid = projectId();
    if (!pid) return;

    // Generate a story ID
    const storyId = `S-${Date.now().toString(36).toUpperCase()}`;

    // Optimistic: add immediately
    const tempStory: Story = {
      id: -Date.now(),
      projectId: pid,
      storyId,
      title,
      priority: 'Medium',
      status,
      createdAt: new Date().toISOString(),
      completedAt: null,
    };
    setStories((prev) => [...prev, tempStory]);

    try {
      const created = await apiCreateStory({
        projectId: pid,
        storyId,
        title,
        priority: 'Medium',
      });
      // Replace temp with real
      setStories((prev) => prev.map((s) => (s.id === tempStory.id ? created : s)));
    } catch (err) {
      console.error('Failed to create story:', err);
      // Remove temp on failure
      setStories((prev) => prev.filter((s) => s.id !== tempStory.id));
    }
  };

  const moveStory = async (storyId: number, toStatus: StoryStatus) => {
    // Optimistic update
    setStories((prev) =>
      prev.map((s) => (s.id === storyId ? { ...s, status: toStatus } : s))
    );

    try {
      const updated = await apiUpdateStory(storyId, { status: toStatus });
      setStories((prev) => prev.map((s) => (s.id === storyId ? updated : s)));
    } catch (err) {
      console.error('Failed to move story:', err);
      // Revert on failure
      refreshStories();
    }
  };

  const updateStoryPriority = async (storyId: number, priority: Priority) => {
    setStories((prev) =>
      prev.map((s) => (s.id === storyId ? { ...s, priority } : s))
    );

    try {
      const updated = await apiUpdateStory(storyId, { priority });
      setStories((prev) => prev.map((s) => (s.id === storyId ? updated : s)));
      // Update selected story
      const sel = selectedStory();
      if (sel && sel.id === storyId) {
        setSelectedStory(updated);
      }
    } catch (err) {
      console.error('Failed to update priority:', err);
      refreshStories();
    }
  };

  const openStoryDetail = (story: Story) => {
    setSelectedStory(story);
  };

  const closeStoryDetail = () => {
    setSelectedStory(null);
  };

  const context: KanbanContextValue = {
    columns,
    stories,
    selectedStory,
    projectId,
    connectionStatus,
    setProject,
    addStory,
    moveStory,
    updateStoryPriority,
    openStoryDetail,
    closeStoryDetail,
    refreshStories,
  };

  return (
    <KanbanContext.Provider value={context}>
      {props.children}
    </KanbanContext.Provider>
  );
};
