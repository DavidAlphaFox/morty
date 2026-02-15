#!/bin/bash
#
# Claude Code Launcher Script
# 用于在指定工作目录下启动 Claude Code
#
# 用法:
#   claude-launcher.sh <工作目录> <plan_mode> <环境变量JSON> <提示词>
#
# 参数:
#   工作目录     - Claude 工作的根目录
#   plan_mode    - "true" 使用 --plan 模式, "false" 使用普通模式
#   环境变量JSON - JSON 格式的环境变量配置，如: '{"KEY1":"value1","KEY2":"value2"}'
#   提示词      - 传给 Claude 的消息内容
#
# 环境变量处理:
#   - 必需变量: 如果变量不存在且 IsRequired=true，脚本会报错退出
#   - 可选变量: 如果变量不存在但有 DefaultValue，使用默认值
#   - 如果都无: 跳过该变量
#

set -e

# 1. 解析参数
if [ $# -lt 4 ]; then
    echo "Usage: $0 <工作目录> <plan_mode> <环境变量JSON> <提示词>"
    echo "Example: $0 /path/to/project true '{\"ANTHROPIC_MODEL\":\"glm-5\"}' '请帮我实现xxx功能'"
    exit 1
fi

WORKING_DIR="$1"
PLAN_MODE="$2"
ENV_JSON="$3"
MESSAGE="$4"

# 2. 验证工作目录
if [ -z "$WORKING_DIR" ]; then
    echo "Error: Working directory is required"
    exit 1
fi

if [ ! -d "$WORKING_DIR" ]; then
    echo "Error: Directory does not exist: $WORKING_DIR"
    exit 1
fi

# 获取绝对路径
WORKING_DIR=$(cd "$WORKING_DIR" && pwd)

# 3. 切换到工作目录并设置安全环境
cd "$WORKING_DIR"

# 4. 解析并设置环境变量
if [ -n "$ENV_JSON" ] && [ "$ENV_JSON" != "null" ]; then
    # 使用 grep 和 sed 解析简单的 JSON (避免依赖 jq)
    # 格式: {"KEY1":"value1","KEY2":"value2"}

    # 移除首尾的大括号和空格
    ENV_JSON="${ENV_JSON#\{}"
    ENV_JSON="${ENV_JSON%\}}"

    # 按逗号分割并处理每个键值对
    IFS=',' read -ra PAIRS <<< "$ENV_JSON"
    for pair in "${PAIRS[@]}"; do
        # 提取键（去掉引号）
        key=$(echo "$pair" | sed 's/.*"\([^"]*\)".*/\1/' | xargs)
        # 提取值（去掉引号）
        value=$(echo "$pair" | sed 's/.*"\([^"]*\)"$/\1/' | xargs)

        if [ -n "$key" ]; then
            export "$key=$value"
            echo "Set environment variable: $key"
        fi
    done
fi

# 5. 在提示词前添加安全限制
SECURE_MESSAGE="注意：你只能在当前目录 ($WORKING_DIR) 及其子目录下工作，不能访问上级目录或目录外的任何文件。
如果需要访问文件，只能使用相对路径或此目录下的绝对路径。

---

$MESSAGE"

# 6. 执行 Claude
if [ "$PLAN_MODE" = "true" ]; then
    # 使用 plan 模式
    echo "$SECURE_MESSAGE" | claude --plan
else
    # 使用普通模式
    echo "$SECURE_MESSAGE" | claude -p
fi
