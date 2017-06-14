using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using System.IO;
using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace GraphCodeSnippetsReflection
{
    class Program
    {

        /// <summary>
        /// 
        /// </summary>
        /// <param name="args">Arg0 MUST be the path the assembly. Arg1 MUST be the input file path.</param>
        static void Main(string[] args)
        {
            // TEST paths are set in project.
            //args[0] = @"C:\repos\GraphCodeSnippetsReflection\packages\Microsoft.Graph.1.3.0\lib\net45\Microsoft.Graph.dll";
            //args[1] = @"C:\repos\GraphCodeSnippetsReflection\input.txt";

            // TODO: Validate input and provide command line instructions.
            if (args.Length != 2)
            {
                Console.WriteLine("Usage:\r\n");
                Console.WriteLine("GraphCodeSnippetsReflection.exe 'path to assembly' 'path to input file'");
                Console.WriteLine("\r\n Press any key to continue");
                Console.Read();
                return;
            }

            if (!args[0].Contains("Microsoft.Graph.dll"))
            {
                Console.WriteLine("You must provide a valid path to Microsoft.Graph.dll");
                Console.WriteLine("\r\n Press any key to continue");
                Console.Read();
                return;
            }
            // TODO: validate input file path.
            
            
            string targetAssembly = args[0];

            List<InputSnippet> inputSnippetList = new List<InputSnippet>();

            using (FileStream fs = File.OpenRead(args[1]))
            using (StreamReader sr = new StreamReader(fs, Encoding.UTF8, true))
            {
                var lineCount = File.ReadLines(args[1]).Count();

                string line;
                int lineNumber = 0;
                while ((line = sr.ReadLine()) != null && lineNumber < lineCount)
                {
                    // TODO: validate inputs.
                    string[] inputSnippets = line.Split(',');
                    inputSnippetList.Add(new InputSnippet(inputSnippets[0], inputSnippets[1]));
                    lineNumber++;
                }
            }

            // TODO: refactor this out
            foreach (InputSnippet s in inputSnippetList)
            {
                string requestUrl = s.UrlToResource;
                string httpVerb = s.HttpVerb;

                // TODO: put this processing in the InputSnippet setter. 
                httpVerb = httpVerb.ToLower();
                httpVerb = char.ToUpper(httpVerb[0]) + httpVerb.Substring(1); // TODO: refactor this out into a separate method.

                // Breakdown the URL payload into parts. We will use this to look up types 
                // that are used to build up the snippet.
                requestUrl = requestUrl.Trim('/');
                string[] requestUrlParts = requestUrl.Split('/');

                // Where we store the snippet parts. We will use this to fill out the templates.
                List<string> snippetParts = new List<string>();



                // Get all of the types in the assembly.
                Assembly assembly = Assembly.LoadFrom(targetAssembly);
                Type[] types = assembly.GetTypes();

                // Create our list of types that are used to build up the code snippet. The typeChain list
                // contains the request builder types used to create the input URL.
                List<Type> typeChain = new List<Type>();
                Type graphServiceClient = types.Where(t => t.Name == "GraphServiceClient")
                                               .Select(t => t)
                                               .First();

                // Initial type in our chain.
                typeChain.Add(graphServiceClient);

                // Process the navigation properties to add to our type chain and snippet parts.
                for (int i = 0; i < requestUrlParts.Length; i++)
                {
                    string propertyName = char.ToUpper(requestUrlParts[i][0]) + requestUrlParts[i].Substring(1);

                    Type propertyType = types.Where(t => t.Name == typeChain[i].Name)
                                             .SelectMany(pl => pl.GetProperties()) // Have all the properties
                                             .Where(p => p.Name == propertyName) // Get property based on Url parts.
                                             .Select(v => v.PropertyType).First();

                    typeChain.Add(propertyType);
                    snippetParts.Add("." + char.ToUpper(requestUrlParts[i][0]) + requestUrlParts[i].Substring(1));
                }

                // Once we get to the end of the property typeChain, we need to select the Request() method return type.
                Type lastType = typeChain[typeChain.Count - 1];
                MethodInfo[] lastTypeMethods = lastType.GetMethods();
                MethodInfo defaultRequestMethod = lastTypeMethods.Where(m => m.Name == "Request")
                                                                 .Select(m => m)
                                                                 .First();

                // Now we have the *Request object. Let's first add the .Request() segment to our
                // snippetParts List.
                snippetParts.Add(".Request()");

                // Now we need to match the input HTTP verb with the right method.
                Type requestObject = defaultRequestMethod.ReturnType;
                MethodInfo[] requestMethods = requestObject.GetMethods();

                // Collections we add to instead of using the http verb.
                if (requestObject.Name.Contains("CollectionRequest") && httpVerb != "Get")
                {
                    httpVerb = "ADD";
                    httpVerb = httpVerb.ToLower();
                    httpVerb = char.ToUpper(httpVerb[0]) + httpVerb.Substring(1); // TODO: refactor this out into a separate method.
                }

                MethodInfo targetHttpMethod = requestMethods.Where(m => m.Name.Contains(httpVerb))
                                                            .Select(m => m)
                                                            .First();

                // Now we add the Http verb method to the snippetParts list.
                snippetParts.Add("." + targetHttpMethod.Name + "()");

                // Set the method return type. We will need to extract it.
                string methodReturnType = targetHttpMethod.ReturnType.FullName;
                string entryPoint = "Microsoft.Graph.";

                int start = methodReturnType.IndexOf(entryPoint);
                start = start + entryPoint.Length;
                int end = methodReturnType.IndexOf(',');
                methodReturnType = methodReturnType.Substring(start, end - start);

                // Construct the code snippet.
                // Templates for filling out snippets.
                string commonSnippet = "GraphServiceClient graphClient = new GraphServiceClient();";
                string snippetTemplate = "{0} {1} = await graphClient{2}";
                string snippetRequestBuilders = "";

                foreach (string sp in snippetParts)
                {
                    snippetRequestBuilders = snippetRequestBuilders + sp;
                }


                string snippet = String.Format(snippetTemplate, methodReturnType, methodReturnType.ToLower(), snippetRequestBuilders);

                Console.WriteLine(commonSnippet);
                Console.WriteLine(snippet);
                Console.WriteLine();
            }

            Console.Read();
        }

        /// <summary>
        /// Use this one as its faster.
        /// https://www.dotnetperls.com/uppercase-first-letter
        /// </summary>
        /// <param name="s"></param>
        /// <returns></returns>
        private string UppercaseFirstLetter(string s)
        {
            if (string.IsNullOrEmpty(s))
            {
                return string.Empty;
            }
            char[] a = s.ToCharArray();
            a[0] = char.ToUpper(a[0]);
            return new string(a);
        }

        private string LowercaseFirstLetter(string s)
        {
            if (string.IsNullOrEmpty(s))
            {
                return string.Empty;
            }
            char[] a = s.ToCharArray();
            a[0] = char.ToLower(a[0]);
            return new string(a);
        }
    }

    internal class InputSnippet
    {
        internal InputSnippet(string httpVerb, string urlToResource)
        {
            this.HttpVerb = httpVerb;
            this.UrlToResource = urlToResource;
        }

        internal string HttpVerb { get; set; }
        internal string UrlToResource { get; set; }
    }

}
