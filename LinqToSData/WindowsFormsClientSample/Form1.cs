using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using Sage.Entity.Interfaces;
using SDataLinqProvider;

namespace WindowsFormsClientSample
{
    public partial class Form1 : Form
    {
        private List<IContact> _contactList;
        private int _contactIndex;

        public Form1()
        {
            InitializeComponent();
            Sage.Platform.Application.ApplicationContext.Initialize("LinqTest");
        }

        private void button1_Click(object sender, EventArgs e)
        {
            var repository = new SDataEntityRepository("http://localhost/sdata/slx/dynamic", "admin", "");
            //IQueryable<IContact> query = from contact in repository.CreateQuery<IContact>()
            //                             where contact.FirstName == txtFirstNameSearch.Text
            //                                //&& contact.YearGraduated > 1960
            //                             select contact;
            //_contactList = query.ToList();

            //if (_contactList.Count == 0)
            //    Text = "No matching entities found";

            //account query
            //var accountList = (from account in repository.CreateQuery<IAccount>()
            //                   where account.Employees > 200
            //                   select account).ToList();

            //Projecting anonymous type
            var miniContacts = from contact in repository.CreateQuery<IContact>()
                               where contact.FirstName == txtFirstNameSearch.Text
                               select new { FN = contact.FirstName, LN = contact.LastName };
            var miniList1 = miniContacts.ToList();

            //query using extension methods
            miniContacts = repository.CreateQuery<IContact>()
                                    .Where(contact2 => contact2.FirstName == txtFirstNameSearch.Text)
                                    .Select(contact2 => new { FN = contact2.FirstName, LN = contact2.LastName });
            var miniList2 = miniContacts.ToList();

            //project anonymous type with string concatenation            
            var contactNames = (from contact3 in repository.CreateQuery<IContact>()
                                select new { FullName = contact3.FirstName + " " + contact3.LastName }).ToList();
            var contactFullNameList = contactNames.ToList();

            //get by Id
            var contactById = repository.GetEntityById<IContact>("CA2EK0013122");
            
            _contactIndex = 0;
            BindCurrentEntity();
        }

        private void BindCurrentEntity()
        {
            bindingSource1.DataSource = _contactList[_contactIndex];
            Text = string.Format("Displaying entity {0} of {1}", _contactIndex + 1, _contactList.Count);
        }

        private void btnNext_Click(object sender, EventArgs e)
        {
            if (_contactList == null)
                return;

            if (_contactIndex < _contactList.Count - 1)
                _contactIndex++;

            BindCurrentEntity();
        }

        private void btnPrevious_Click(object sender, EventArgs e)
        {
            if (_contactList == null)
                return;

            if (_contactIndex > 0)
                _contactIndex--;

            BindCurrentEntity();
        }

        private void btnTestCRUD_Click(object sender, EventArgs e)
        {
            const string address1 = "My Street";
            var repository = new SDataEntityRepository("http://localhost/sdata/slx/dynamic", "admin", "");
            IAddress newAddress = repository.Create<IAddress>();
            newAddress.Address1 = address1;
            newAddress.City = "Scottsdale";
            newAddress.State = "Arizona";
            newAddress.PostalCode = "85258";
            newAddress.EntityId = "000000000000";
            newAddress.Description = "This is a description";
            repository.Save(newAddress);

            var addressQuery = from address in repository.CreateQuery<IAddress>()
                               where address.Address1 == address1
                               select address;
            var getAddress = addressQuery.ToList().First();            
            repository.Delete(getAddress);
        }
    }
}
