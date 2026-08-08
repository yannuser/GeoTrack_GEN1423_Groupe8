namespace GeoTrack.Api.Services
{
    /// <summary>
    /// GEO-58 : implementation de production d'<see cref="INotificationService"/>,
    /// consommee par <see cref="AlerteVitesseService"/> (GEO-51).
    ///
    /// Aucun envoi reel : chaque canal (push, SMS, courriel, tableau de bord) est
    /// journalise via ILogger. C'est deliberé — brancher un fournisseur reel
    /// (Twilio, SMTP, SignalR) est un chantier a part entiere, et le service
    /// GEO-51 avait besoin d'une implementation pour etre enfin injectable.
    /// Chaque canal garde sa propre trace, de sorte que la trajectoire
    /// d'escalade (push seul, puis + courriel, puis + SMS) reste lisible dans
    /// les journaux.
    ///
    /// Sans dependance a duree de vie courte : ILogger est Singleton, cette
    /// classe l'est donc aussi. C'est indispensable, puisque AlerteVitesseService
    /// est lui-meme Singleton et ne peut pas capturer un service Scoped.
    /// </summary>
    public class NotificationServiceJournal : INotificationService
    {
        private readonly ILogger<NotificationServiceJournal> _journal;

        public NotificationServiceJournal(ILogger<NotificationServiceJournal> journal)
        {
            _journal = journal;
        }

        public Task EnvoyerPush(string appareilId, string message, SeveriteAlerte severite)
        {
            _journal.LogWarning(
                "[PUSH] Appareil {AppareilId} — severite {Severite} : {Message}",
                appareilId, severite, message);

            return Task.CompletedTask;
        }

        public Task EnvoyerSms(string appareilId, string message)
        {
            _journal.LogWarning(
                "[SMS] Appareil {AppareilId} : {Message}",
                appareilId, message);

            return Task.CompletedTask;
        }

        public Task EnvoyerEmail(string appareilId, string message)
        {
            _journal.LogWarning(
                "[COURRIEL] Appareil {AppareilId} : {Message}",
                appareilId, message);

            return Task.CompletedTask;
        }

        public Task EnvoyerDashboard(string appareilId, ResultatEvaluation resultat)
        {
            _journal.LogInformation(
                "[TABLEAU DE BORD] Appareil {AppareilId} — etat {Etat}, severite {Severite}, "
                + "vitesse mesuree {VitesseMesuree} km/h pour un seuil de {SeuilDepasse} km/h. "
                + "Motif : {Raison}.",
                appareilId, resultat.Etat, resultat.Severite,
                resultat.VitesseMesuree, resultat.SeuilDepasse, resultat.Raison);

            return Task.CompletedTask;
        }
    }
}
