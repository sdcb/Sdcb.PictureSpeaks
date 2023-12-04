// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.
function lobbyStatusToString(lobbyState) {
    if (lobbyState === 0) return "等待中……";
    if (lobbyState === 1) return "就绪";
    if (lobbyState === 2) return "错误";
}