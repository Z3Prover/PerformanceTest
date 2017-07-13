using Measurement;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Hosting;
using System.Diagnostics;

namespace PerformanceTest
{
    public interface IDomainResolver
    {
        string[] Domains { get; }

        Domain GetDomain(string domainName);
    }

    public class DomainResolver : IDomainResolver
    {
        protected readonly List<Domain> domains;

        public DomainResolver(IEnumerable<Domain> domains)
        {
            if (domains == null) throw new ArgumentNullException("domains");
            this.domains = domains.ToList();
        }

        public string[] Domains
        {
            get
            {
                return domains.Select(d => d.Name).ToArray();
            }
        }

        public Domain GetDomain(string domainName)
        {
            if (domainName == null) throw new ArgumentNullException("domainName");
            foreach (var d in domains)
            {
                if (d.Name == domainName)
                {
                    return d;
                }
            }
            throw new KeyNotFoundException(String.Format("Domain '{0}' not found", domainName));
        }
    }

    public class MEFDomainResolver : IDomainResolver
    {
        private static IDomainResolver instance;

        public static IDomainResolver Instance
        {
            get
            {
                if (instance == null)
                    instance = new MEFDomainResolver();
                return instance;
            }
        }


        [ImportMany(typeof(Domain))]
        protected List<Domain> domains;

        public MEFDomainResolver() : this(System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location))
        {
        }

        public MEFDomainResolver(string directoryPath)
        {
            var catalog = new AggregateCatalog();
            catalog.Catalogs.Add(new DirectoryCatalog(directoryPath, "*Domain.dll"));

            try
            {
                var container = new CompositionContainer(catalog);
                container.ComposeParts(this);
            }
            catch (CompositionException ex)
            {
                Trace.WriteLine("Composition exception when resolving domains: " + ex);
            }
        }


        public string[] Domains
        {
            get
            {
                return domains != null ? domains.Select(d => d.Name).ToArray() : new string[0];
            }
        }

        public Domain GetDomain(string domainName)
        {
            if (domainName == null) throw new ArgumentNullException("domainName");
            if (domains != null)
                foreach (var d in domains)
                {
                    if (d.Name == domainName)
                    {
                        return d;
                    }
                }
            throw new KeyNotFoundException(String.Format("Domain '{0}' not found", domainName));
        }
    }
}
