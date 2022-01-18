using ActiveUp.Net.Mail;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace PrijemRemont
{
    public class MailRepository
    {
        private Imap4Client client;
        private System.Net.Mail.SmtpClient senderClient;

        private string mailServer = "imap.gmail.com";
        private int port = 993;
        private bool ssl = true;
        private string email = "projekat3cloud@gmail.com";
        private string password = "masterprojekat3";

        private string successMessage = "Zahtev prihvacen. Uredjaj poslat na remont.\n\n";
        private string errorMessage = "Zahtev odbijen. Uredjaj nije poslat na remont.\n\n Ocekivan format poruke:\n device: (<id> or <name>) \n warehouseTime: <number> \n workHours: <number>.\n\n";
        private string validationMessage = "Zahtev odbijen. Moguci razlozi:\n 1.Uredjaj ne postoji u sistemu.\n 2.Niste dobro uneli podatke.\n 3.Uredjaj se nalazi na remontu.\n\n Ocekivan format poruke:\n device: (<id> or <name>) \n warehouseTime: <number> \n workHours: <number>.\n\n";


        public MailRepository()
        {
            if (ssl)
                Client.ConnectSsl(mailServer, port);
            else
                Client.Connect(mailServer, port);

            Client.Login(email, password);


        }

        public IEnumerable<Message> GetAllMails(string mailBox)
        {
            return GetMails(mailBox, "ALL").Cast<Message>();
        }

        public Task<Tuple<List<string>, List<string>>> GetBodyFromUnreadMails()
        {
            var emailList = GetUnreadMails("inbox");
            List<string> bodyContents = emailList.Select(x => x.BodyText.Text).ToList();
            List<string> sendersEmail = emailList.Select(x => x.From.Email).ToList();

            return Task.FromResult<Tuple<List<string>, List<string>>>(new Tuple<List<string>, List<string>>(sendersEmail, bodyContents));
            //return Task.FromResult<List<string>>(emailList.Select(x => x.BodyText.Text).ToList());

        }

        public async Task SendEmail(string toAddress, string recievedEmail, int indikator)
        {
            //indikator vrednosti
            // 0 - uredjaj poslat na remont
            // 1 - validacija nije prosla ili je uredjaj vec na remontu
            // 2 - format zahteva nije dobar

            try
            {
                string message = string.Empty;

                switch (indikator)
                {
                    case 0:
                        message = successMessage + recievedEmail;
                        await SenderClient.SendMailAsync(email, toAddress, "Zahtev za remont", message);
                        break;

                    case 1:
                        message = validationMessage + "Dobijen zahtev:\n" + recievedEmail;
                        await SenderClient.SendMailAsync(email, toAddress, "Zahtev za remont", message);
                        break;

                    case 2:
                        message = errorMessage + "Dobijen zahtev:\n" + recievedEmail;
                        await SenderClient.SendMailAsync(email, toAddress, "Zahtev za remont", message);
                        break;

                    default:
                        break;
                }
            }
            catch (Exception e)
            {
                string a = e.Message;
            }
        }

        private IEnumerable<Message> GetUnreadMails(string mailBox)
        {
            return GetMails(mailBox, "UNSEEN").Cast<Message>();
        }

        protected Imap4Client Client
        {
            get { return client ?? (client = new Imap4Client()); }
        }

        protected System.Net.Mail.SmtpClient SenderClient
        {
            get
            {
                return senderClient ?? (senderClient = new System.Net.Mail.SmtpClient("smtp.gmail.com", 587)
                {

                    Credentials = new NetworkCredential(email, password),
                    EnableSsl = true,
                });
            }
        }

        private MessageCollection GetMails(string mailBox, string searchPhrase)
        {
            Mailbox mails = Client.SelectMailbox(mailBox);
            MessageCollection messages = mails.SearchParse(searchPhrase);
            return messages;
        }

    }
}
