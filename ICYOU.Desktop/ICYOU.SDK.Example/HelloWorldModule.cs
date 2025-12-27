using ICYOU.SDK;

namespace ICYOU.SDK.Example;

/// <summary>
/// Пример модуля - добавляет команду /hello
/// </summary>
[ModuleInfo("example.helloworld", "Hello World", "1.0.0", 
    Author = "ICYOU Team", 
    Description = "Пример модуля - добавляет команду /hello")]
public class HelloWorldModule : ModuleBase
{
    protected override void OnInitialize()
    {
        Logger.Info("HelloWorld модуль инициализирован!");
        
        // Подписываемся на входящие сообщения
        Subscribe<MessageReceivedEvent>(OnMessageReceived);
        
        // Регистрируем перехватчик исходящих сообщений
        Messages.RegisterOutgoingInterceptor(OnOutgoingMessage);
    }
    
    protected override void OnShutdown()
    {
        Logger.Info("HelloWorld модуль выгружен");
    }
    
    private void OnMessageReceived(MessageReceivedEvent evt)
    {
        var msg = evt.Message;
        
        // Обрабатываем команду /hello
        if (msg.Content.StartsWith("/hello"))
        {
            var name = msg.Content.Length > 7 ? msg.Content.Substring(7).Trim() : "мир";
            Messages.SendMessageAsync(msg.ChatId, $"Привет, {name}! 👋");
        }
    }
    
    private Message? OnOutgoingMessage(Message message)
    {
        // Пример модификации исходящего сообщения
        // Можно вернуть null чтобы заблокировать отправку
        
        // Добавляем подпись к сообщениям (пример)
        // message.Content += " [sent via HelloWorld]";
        
        return message;
    }
}

