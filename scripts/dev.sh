#!/bin/bash
#
# Morty 开发环境快速启动脚本
# 自动启动后端 API 服务和前端开发服务器
#
# 用法:
#   ./scripts/dev.sh          # 启动完整开发环境
#   ./scripts/dev.sh --help   # 显示帮助信息
#

set -e

# 颜色定义
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
MAGENTA='\033[0;35m'
CYAN='\033[0;36m'
NC='\033[0m' # No Color

# 项目路径
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"
BACKEND_DIR="$PROJECT_ROOT/src/Morty.Web"
FRONTEND_DIR="$BACKEND_DIR/ClientApp"

# PID 文件
BACKEND_PID=""
FRONTEND_PID=""

# 打印带颜色的消息
print_info() {
    echo -e "${BLUE}ℹ${NC} $1"
}

print_success() {
    echo -e "${GREEN}✓${NC} $1"
}

print_warning() {
    echo -e "${YELLOW}⚠${NC} $1"
}

print_error() {
    echo -e "${RED}✗${NC} $1"
}

print_header() {
    echo -e "${MAGENTA}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
    echo -e "${MAGENTA}  $1${NC}"
    echo -e "${MAGENTA}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
}

# 显示帮助信息
show_help() {
    echo "Morty 开发环境启动脚本"
    echo ""
    echo "用法:"
    echo "  ./scripts/dev.sh          启动完整开发环境（后端 + 前端）"
    echo "  ./scripts/dev.sh --help   显示此帮助信息"
    echo ""
    echo "说明:"
    echo "  - 后端服务：http://localhost:5000"
    echo "  - 前端开发服务器：http://localhost:5173"
    echo "  - 按 Ctrl+C 可以同时停止后端和前端服务"
    echo ""
}

# 检查命令是否存在
check_command() {
    if ! command -v "$1" &> /dev/null; then
        print_error "$1 未安装，请先安装 $2"
        exit 1
    fi
}

# 检查依赖
check_dependencies() {
    print_header "检查依赖"

    check_command "dotnet" ".NET SDK (https://dotnet.microsoft.com/download)"
    print_success "dotnet $(dotnet --version)"

    check_command "node" "Node.js (https://nodejs.org)"
    print_success "node $(node --version)"

    check_command "npm" "npm (通常随 Node.js 一起安装)"
    print_success "npm $(npm --version)"

    echo ""
}

# 安装前端依赖
install_frontend_deps() {
    print_header "检查前端依赖"

    cd "$FRONTEND_DIR"

    if [ ! -d "node_modules" ]; then
        print_info "node_modules 不存在，开始安装依赖..."
        npm install
        print_success "前端依赖安装完成"
    else
        print_success "前端依赖已存在"
    fi

    echo ""
}

# 启动后端服务
start_backend() {
    print_header "启动后端服务"
    print_info "工作目录: $BACKEND_DIR"
    print_info "服务地址: http://localhost:5000"

    cd "$BACKEND_DIR"

    # 使用 dotnet run 启动，输出带前缀
    dotnet run --urls "http://localhost:5000" 2>&1 | sed -e "s/^/[${CYAN}BACKEND${NC}] /" &
    BACKEND_PID=$!

    print_success "后端服务已启动 (PID: $BACKEND_PID)"
    echo ""
}

# 启动前端开发服务器
start_frontend() {
    print_header "启动前端开发服务器"
    print_info "工作目录: $FRONTEND_DIR"
    print_info "开发服务器: http://localhost:5173"

    cd "$FRONTEND_DIR"

    # 使用 npm run dev 启动，输出带前缀
    npm run dev 2>&1 | sed -e "s/^/[${GREEN}FRONTEND${NC}] /" &
    FRONTEND_PID=$!

    print_success "前端开发服务器已启动 (PID: $FRONTEND_PID)"
    echo ""
}

# 清理函数 - 停止所有服务
cleanup() {
    echo ""
    print_header "正在停止服务"

    if [ -n "$BACKEND_PID" ]; then
        print_info "停止后端服务 (PID: $BACKEND_PID)..."
        kill $BACKEND_PID 2>/dev/null || true
        wait $BACKEND_PID 2>/dev/null || true
        print_success "后端服务已停止"
    fi

    if [ -n "$FRONTEND_PID" ]; then
        print_info "停止前端开发服务器 (PID: $FRONTEND_PID)..."
        kill $FRONTEND_PID 2>/dev/null || true
        wait $FRONTEND_PID 2>/dev/null || true
        print_success "前端开发服务器已停止"
    fi

    echo ""
    print_success "开发环境已关闭"
    exit 0
}

# 主函数
main() {
    # 处理参数
    if [ "$1" == "--help" ] || [ "$1" == "-h" ]; then
        show_help
        exit 0
    fi

    # 设置 trap 以便 Ctrl+C 时清理
    trap cleanup SIGINT SIGTERM

    # 显示欢迎信息
    clear
    print_header "🚀 Morty 开发环境启动"
    echo ""

    # 执行启动流程
    check_dependencies
    install_frontend_deps
    start_backend

    # 等待后端启动
    print_info "等待后端服务启动..."
    sleep 3

    start_frontend

    # 显示总结信息
    print_header "✨ 开发环境已就绪"
    echo ""
    echo -e "  ${GREEN}后端 API:${NC}      http://localhost:5000"
    echo -e "  ${GREEN}前端界面:${NC}      http://localhost:5173"
    echo -e "  ${GREEN}API 文档:${NC}      http://localhost:5000/swagger (如果配置了)"
    echo ""
    print_warning "按 Ctrl+C 停止所有服务"
    echo ""
    print_header "服务日志"
    echo ""

    # 等待进程
    wait
}

# 运行主函数
main "$@"
