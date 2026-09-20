namespace BKeeper.Application.Notifications;

/// <summary>
/// The fixed template set from plan §10. Text is warm, never references data the member didn't
/// provide (D-rule in §10: no "we noticed you missed 3 classes"). Variables are substituted from an
/// explicit whitelist only — an unknown {placeholder} is left as literal text rather than silently
/// dropped, so a bad template shows up immediately instead of shipping a broken message.
/// </summary>
public static class TemplateCatalog
{
    public const string Welcome = "WELCOME";
    public const string OnbNudgeD3 = "ONB_NUDGE_D3";
    public const string MissYouSoft = "MISS_YOU_SOFT";
    public const string ScheduleNudge = "SCHEDULE_NUDGE";
    public const string Milestone50 = "MILESTONE_50";
    public const string Anniversary = "ANNIVERSARY";
    public const string EvalRequest = "EVAL_REQUEST";
    public const string EvalReminder = "EVAL_REMINDER";
    public const string RenewalReminder = "RENEWAL_REMINDER";
    public const string WinbackD30 = "WINBACK_D30";

    private static readonly Dictionary<string, (string PtPt, string En)> Templates = new()
    {
        [Welcome] = ("Olá {first_name}! Bem-vindo(a) à {box_name} 💪 Qualquer dúvida, fala com {coach}.",
                     "Hi {first_name}! Welcome to {box_name} 💪 Reach out to {coach} with any questions."),
        [OnbNudgeD3] = ("Olá {first_name}, ainda não te vimos por aqui — queres marcar a tua primeira aula?",
                        "Hi {first_name}, we haven't seen you yet — want to book your first class?"),
        [MissYouSoft] = ("Olá {first_name}, sentimos a tua falta na {box_name}! A tua turma de {usual_class} está à tua espera.",
                         "Hi {first_name}, we miss you at {box_name}! Your {usual_class} class is waiting for you."),
        [ScheduleNudge] = ("Olá {first_name}, já pensaste em marcar a tua próxima aula de {usual_class}?",
                           "Hi {first_name}, thinking about booking your next {usual_class} class?"),
        [Milestone50] = ("Parabéns {first_name}! 🎉 Já celebraste mais uma marca de treinos na {box_name}.",
                         "Congrats {first_name}! 🎉 You just hit another training milestone at {box_name}."),
        [Anniversary] = ("Olá {first_name}, já é um ano contigo na {box_name} — obrigado por fazeres parte disto!",
                         "Hi {first_name}, it's been a year with you at {box_name} — thanks for being part of it!"),
        [EvalRequest] = ("Olá {first_name}, ajuda-nos a melhorar: {form_link}",
                         "Hi {first_name}, help us improve: {form_link}"),
        [EvalReminder] = ("Olá {first_name}, ainda vais a tempo de responder: {form_link}",
                          "Hi {first_name}, there's still time to answer: {form_link}"),
        [RenewalReminder] = ("Olá {first_name}, a tua inscrição na {box_name} renova em breve.",
                             "Hi {first_name}, your membership at {box_name} renews soon."),
        [WinbackD30] = ("Olá {first_name}, sentimos a tua falta na {box_name}. Adoravamos ter-te de volta!",
                        "Hi {first_name}, we miss you at {box_name}. We'd love to have you back!"),
    };

    public static bool TryRender(string templateKey, string language, IReadOnlyDictionary<string, string> variables, out string body)
    {
        body = string.Empty;
        if (!Templates.TryGetValue(templateKey, out var text)) return false;

        var template = language.StartsWith("pt", StringComparison.OrdinalIgnoreCase) ? text.PtPt : text.En;
        body = variables.Aggregate(template, (current, kv) => current.Replace("{" + kv.Key + "}", kv.Value));
        return true;
    }
}
