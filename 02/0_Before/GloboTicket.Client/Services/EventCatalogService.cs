using GloboTicket.Web.Extensions;
using GloboTicket.Web.Models.Api;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;

namespace GloboTicket.Web.Services
{
    public class EventCatalogService : IEventCatalogService
    {
        private readonly HttpClient client;

        public EventCatalogService(HttpClient client)
        {
            this.client = client;
        }

        public async Task<IEnumerable<Event>> GetAll()
        {
            var response = await client.GetAsync("/api/events");

            if (response.IsSuccessStatusCode)
            {
                return await response.ReadContentAs<List<Event>>();
            }

            return Array.Empty<Event>();
        }

        public async Task<IEnumerable<Event>> GetByCategoryId(Guid categoryid)
        {
            try
            {
                var response = await client.GetAsync($"/api/events/?categoryId={categoryid}");
                return await response.ReadContentAs<List<Event>>();
            }
            catch (Exception e)
            {
                // todo
            }

            return Array.Empty<Event>();
        }

        public async Task<Event> GetEvent(Guid id)
        {
            try
            {  
                var response = await client.GetAsync($"/api/events/{id}");

                if (response.IsSuccessStatusCode)
                {
                   
                    return await response.ReadContentAs<Event>();
                }
            }
            catch (Exception e)
            {
                // todo
            }

            return null;
        }

        public async Task<IEnumerable<Category>> GetCategories()
        {
            try
            {
                var response = await client.GetAsync("/api/categories");
                return await response.ReadContentAs<List<Category>>();
            }
            catch (Exception e)
            {
                // todo
            }

            return Array.Empty<Category>();
        }
    }
}
