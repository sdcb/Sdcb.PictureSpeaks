ko.bindingHandlers.lobbyStatus = {
    update: function (element, valueAccessor) {
        var value = ko.utils.unwrapObservable(valueAccessor());
        var text = lobbyStatusToString(value);
        var color = '';

        switch (value) {
            case 0:
                color = 'grey';  // 等待中
                break;
            case 1:
                color = 'green';  // 就绪
                break;
            case 2:
                color = 'red';  // 错误
                break;
            case 3:
                color = 'blue';  // 已完成
                break;
            default:
                color = 'black';  // 默认颜色，你也可以设置为其他颜色
                break;
        }

        element.style.color = color; // 设置元素的文本颜色
        element.textContent = text;  // 更新元素的文本
    }
};

function lobbyStatusToString(lobbyState) {
    if (lobbyState === 0) return "等待中……";
    if (lobbyState === 1) return "就绪";
    if (lobbyState === 2) return "错误";
    if (lobbyState === 3) return "已完成";
}

function randomName() {
    const names = [
        "朱铁志",
        "苏山河",
        "萧风月",
        "何铁柱",
        "柳长空",
        "程百川",
        "温浩然",
        "袁天锋",
        "蓝玉强",
        "邵飞云",
        "庞雷音",
        "秦无痕",
        "姚星海",
        "傅剑波",
        "宋霜叶",
        "白浪客",
        "范翔羽",
        "武羲尘",
        "尹风怒",
        "聂远山",
        "丁铁君",
        "桂三峰",
        "夏侯飞絮",
        "华铭晨",
        "戚绝尘",
        "樊翼虎",
        "申屠霜",
        "江流石",
        "纪飞虹",
        "顾盘山",
        "荆紫电",
        "卓不群",
        "罗天朗",
        "宗遁空",
        "辛万里",
        "周银龙",
        "泰山客",
        "杜云鹏",
        "尤风魔",
        "蒋泽天",
        "费冷月",
        "阎一翔",
        "韩无声",
        "赵破虚",
        "赖明刚",
        "翟天赋",
        "龚云起",
        "薛山河",
        "范明刚",
        "司空影青",
    ];

    return names[Math.floor(Math.random() * names.length)];
}