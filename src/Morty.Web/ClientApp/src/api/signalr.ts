/**
 * SignalR 模块
 * 提供与后端的实时双向通信
 * 用于接收故事更新和迭代完成等事件的实时推送
 */

import * as signalR from '@microsoft/signalr';
import type { Story, Iteration } from '../types';

/**
 * SignalR 回调函数接口
 * 定义所有可订阅的事件处理函数
 */
export interface SignalRCallbacks {
  /** 当故事更新时触发 */
  onStoryUpdated?: (story: Story) => void;
  /** 当迭代完成时触发 */
  onIterationComplete?: (iteration: Iteration) => void;
  /** 当连接成功时触发 */
  onConnected?: () => void;
  /** 当连接断开时触发 */
  onDisconnected?: () => void;
  /** 当正在重新连接时触发 */
  onReconnecting?: () => void;
}

/**
 * 创建 SignalR 连接
 * 封装 SignalR Hub 连接的建立、事件绑定和项目管理
 * @param callbacks 事件回调函数对象
 * @returns 连接控制对象，包含 start、stop、joinProject、leaveProject 方法
 */
export function createSignalRConnection(callbacks: SignalRCallbacks) {
  // 构建 SignalR 连接，配置自动重连
  const connection = new signalR.HubConnectionBuilder()
    .withUrl('/morty-hub')  // Hub 端点 URL
    .withAutomaticReconnect()
    .build();

  // 订阅故事更新事件
  connection.on('OnStoryUpdated', (story: Story) => {
    callbacks.onStoryUpdated?.(story);
  });

  // 订阅迭代完成事件
  connection.on('OnIterationComplete', (iteration: Iteration) => {
    callbacks.onIterationComplete?.(iteration);
  });

  // 订阅重新连接事件
  connection.onreconnecting(() => {
    callbacks.onReconnecting?.();
  });

  // 订阅重连成功事件
  connection.onreconnected(() => {
    callbacks.onConnected?.();
  });

  // 订阅连接关闭事件
  connection.onclose(() => {
    callbacks.onDisconnected?.();
  });

  // 返回连接控制接口
  return {
    /**
     * 启动 SignalR 连接
     * 异步连接到 Hub 服务器
     */
    async start() {
      try {
        await connection.start();
        callbacks.onConnected?.();
      } catch (err) {
        console.error('SignalR connection error:', err);
        callbacks.onDisconnected?.();
      }
    },

    /**
     * 停止 SignalR 连接
     * 断开与 Hub 服务器的连接
     */
    async stop() {
      await connection.stop();
    },

    /**
     * 加入项目房间
     * 订阅特定项目的消息推送
     * @param projectId 项目 ID
     */
    async joinProject(projectId: number) {
      try {
        await connection.invoke('JoinProject', projectId);
      } catch {
        // JoinProject 方法可能不存在，忽略错误
      }
    },

    /**
     * 离开项目房间
     * 取消订阅特定项目的消息推送
     * @param projectId 项目 ID
     */
    async leaveProject(projectId: number) {
      try {
        await connection.invoke('LeaveProject', projectId);
      } catch {
        // LeaveProject 方法可能不存在，忽略错误
      }
    },

    // 暴露原始连接对象
    connection,
  };
}
