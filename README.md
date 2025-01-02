# Sdcb.PictureSpeaks [![QQ](https://img.shields.io/badge/QQ_Group-495782587-52B6EF?style=social&logo=tencent-qq&logoColor=000&logoWidth=20)](http://qm.qq.com/cgi-bin/qm/qr?_wv=1027&k=mma4msRKd372Z6dWpmBp4JZ9RL4Jrf8X&authKey=gccTx0h0RaH5b8B8jtuPJocU7MgFRUznqbV%2FLgsKdsK8RqZE%2BOhnETQ7nYVTp1W0&noverify=0&group_code=495782587)

Sdcb.PictureSpeaks 是一个根据AI生成的图片，和AI互动，猜成语的游戏demo。

它基于`.NET 8`构建的`ASP.NET MVC`，`knockout.js`实现MVVM模式，`SignalR`用于实时流式聊天通信。

本项目使用`Azure OpenAI`的`ChatGPT`及`DALL·E3`，为用户提供聊天和AI辅助服务。

此外，本项目采用`Entity Framework Core 8.0`进行数据管理，并使用`SQLite`作为数据库解决方案。

## 功能特色

- **AI聊天与辅助**：集成Azure OpenAI的技术，使聊天体验更加智能化。
  * 你可以向AI请求更多提示，比如说“这个图上我看不太出来，你能提供一些相关的提示吗？”
  * 你可以申请向AI生成一张新图片，比如说“这个图有点太难了，能生成一张新图片吗？”
- **多人实时聊天**：基于SignalR的实时流式聊天功能，让沟通变得更加即时和高效。
- **MVVM前端设计**：使用knockout.js实现前端的MVVM设计模式，简化数据与UI间的互动（当然这是老技术）。
- **数据持久化**：利用最新版的Entity Framework Core 8.0与SQLite实现稳定的数据存储和访问。

## 运行前的配置

在`appsettings.json`或`userSecrets.json`中，输入Azure OpenAI的api key即可：
```json
{
  "AzureOpenAI": {
    "Endpoint": "https://{{your-api-key}}.openai.azure.com",
    "ApiKey": "change-into-your-api-key"
  }
}

```

## 在线演示

您可以通过以下链接访问在线演示网站：[Sdcb.PictureSpeaks在线演示](https://ps.starworks.cc:88)

## 效果图

![image](https://github.com/sdcb/Sdcb.PictureSpeaks/assets/1317141/50a672c8-0511-442c-acc7-1e1db557c347)
![image](https://github.com/sdcb/Sdcb.PictureSpeaks/assets/1317141/e9b625db-a0e9-40c0-8be8-39fbcee79fd8)

## 许可证

本项目采用MIT许可证授权。有关更多信息，请参阅[LICENSE](./LICENSE)文件。