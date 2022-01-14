using ActiveUp.Net.Mail;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PrijemRemont
{
    public class MailRepository
    {
        private Imap4Client client;

        private string mailServer = "imap.gmail.com";
        private int port = 993;
        private bool ssl = true;
        private string email = "projekat3cloud@gmail.com";
        private string password = "masterprojekat3";

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

        public async Task<List<string>> GetBodyFromUnreadMails()
        {
            var emailList = GetUnreadMails("inbox");
            return emailList.Select(x => x.BodyText.Text).ToList();
        }

        private IEnumerable<Message> GetUnreadMails(string mailBox)
        {
            return GetMails(mailBox, "UNSEEN").Cast<Message>();
        }

        protected Imap4Client Client
        {
            get { return client ?? (client = new Imap4Client()); }
        }

        private MessageCollection GetMails(string mailBox, string searchPhrase)
        {
            Mailbox mails = Client.SelectMailbox(mailBox);
            MessageCollection messages = mails.SearchParse(searchPhrase);
            return messages;
        }

    }
}
