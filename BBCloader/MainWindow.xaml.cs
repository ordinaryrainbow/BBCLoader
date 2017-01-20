using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Net;
using System.Xml.Linq;
using System.Xml;
using System.Data;
using System.Data.SqlClient;
using MySql.Data.MySqlClient;
using System.Diagnostics;
using System.Xml.XPath;
using System.Web;
using HtmlAgilityPack;
using System.Collections.Specialized;
using System.Collections.ObjectModel;
using System.Windows.Threading;
using VDS.RDF;
using VDS.RDF.Parsing;
using VDS.Common;

namespace BBCloader
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    /// 

    public partial class MainWindow : Window
    {

        public class MTObservableCollection<T> : ObservableCollection<T>
        {
            public override event NotifyCollectionChangedEventHandler CollectionChanged;
            protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
            {
                NotifyCollectionChangedEventHandler CollectionChanged = this.CollectionChanged;
                if (CollectionChanged != null)
                    foreach (NotifyCollectionChangedEventHandler nh in CollectionChanged.GetInvocationList())
                    {
                        DispatcherObject dispObj = nh.Target as DispatcherObject;
                        if (dispObj != null)
                        {
                            Dispatcher dispatcher = dispObj.Dispatcher;
                            if (dispatcher != null && !dispatcher.CheckAccess())
                            {
                                dispatcher.BeginInvoke(
                                    (Action)(() => nh.Invoke(this,
                                        new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset))),
                                    DispatcherPriority.DataBind);
                                continue;
                            }
                        }
                        nh.Invoke(this, e);
                    }
            }
        }

        private MTObservableCollection<LogLine> loglist;
        private Dictionary<Uri, HttpStatusCode> returnCodes;

        List<ProgramEpisode> AZprogramList;
        Dictionary<String, List<Tuple<String, String>>> CategoryList;

        public class LogList : MTObservableCollection<LogLine>
        {
            public LogList()
                : base()
            {

            }

        };

        public MainWindow()
        {
            loglist = new LogList();


            InitializeComponent();
            AZprogramList = new List<ProgramEpisode>();
            CategoryList = new Dictionary<String, List<Tuple<String, String>>>();
            returnCodes = new Dictionary<Uri, HttpStatusCode>();

            ItemCollection collection = episodesDataGrid.Items;
        }

        private void OPMLButton_Click(object sender, RoutedEventArgs e)
        {

            string savedFile = @"C:\Users\Public\TestFolder\OPML.xml";

            String content;

            if (!File.Exists(savedFile))
            {

                string url = @"http://www.bbc.co.uk/radio/opml/bbc_podcast_opml.opml";
                System.Net.WebClient webclient = new WebClient();
                content = webclient.DownloadString(url);

                TextReader sr = new StringReader(content);
                XmlDocument cleanXML = FromHtml(sr);
                cleanXML.Save(savedFile);

            }

            else
            {
                content = System.IO.File.ReadAllText(savedFile);
            }

            XDocument XMLDoc = XDocument.Parse(content);

            IEnumerable<XElement> radioStations =
                XMLDoc.Root
                .Elements("body")
                .Elements("outline")
                .Elements("outline");

            foreach (var element in radioStations)
            {

                String thisStation = (String)element.Attribute("text");

                IEnumerable<XElement> thisStationShows = element.Elements("outline");

                foreach (var show in thisStationShows)
                {
                    String thisShow = (String)show.Attribute("text");
                    String description = (String)show.Attribute("description");
                    String imageHref = (String)show.Attribute("imageHref");
                    String typicalDurationMins = (String)show.Attribute("typicalDurationMins");
                    String xmlUrl = (String)show.Attribute("xmlUrl");
                    String htmlUrl = (String)show.Attribute("htmlUrl");
                    String keyname = (String)show.Attribute("keyname");
                    String allow = (String)show.Attribute("allow");
                    String active = (String)show.Attribute("active");
                    String page = (String)show.Attribute("page");
                    String flavour = (String)show.Attribute("flavour");
                    String bbcgenres = (String)show.Attribute("bbcgenres");
                }
            }


        }

        private void RetrieveEpisiodesFromProgrammeClick(object sender, RoutedEventArgs e)
        {
            retrieveProgrammeInformation("b006qgt7");
            retrieveEpisodesForProgramme("now show","b006qgt7");   //b00j9k3c   : now show
        }


        // in with now show programme -> get episodes plus series, add to programme lists plus episode lookups

        private void retrieveEpisodesForProgramme(string programmeName,string programmeID)
        {
            int pageCount = 1;
            int thisPage = 1;
            
            for (;;)                    // loop through all pages for this programme
            {

                if (thisPage > pageCount)
                    break;

                List<Tuple<string, string>> foundEpisodeProgrammeIDs = new List<Tuple<string, string>>();
                int tentativePageCount = retrieveEpisodesForProgrammePerPage(programmeName,programmeID, thisPage, foundEpisodeProgrammeIDs);

                if (thisPage == 1)
                    pageCount = tentativePageCount;

                // now work down through descendent episodes for this progamme, this page

                foreach (Tuple<string, string> episodeTuple in foundEpisodeProgrammeIDs)
                {
                    retrieveEpisodesForProgramme(episodeTuple.Item2,episodeTuple.Item1);
                }

                thisPage++;
            }

        }


// Raw episodes, no series
//
//<li class="episode alt">    
//    <div class="episode-item" resource="/programmes/b00w8vzm#programme" typeof="po:Episode"> 
//        <div class="summary">  
//            <h4> 
//                <a href="/programmes/b00w8vzm" about="/programmes/b00w8vzm#programme"> 
//                    <span class="title" property="dc:title">28/11/2010</span> 
//                </a> 
//            </h4>  
//            <p class="synopsis">  
//                <span property="po:short_synopsis">Steve Punt and Hugh Dennis take a satirical look at the week's news from 19 November 2010.
//                </span> 
//            </p>   
//            <p class="first-broadcast"> 
//                <a href="/programmes/b00w8vzm#programme-broadcasts">First broadcast</a>: 28 Nov 2010 
//            </p>  
//        </div> 
//        <div rel="foaf:depiction" class="depiction"> 
//            <img src="http://ichef.bbci.co.uk/images/ic/256x144/legacy/episode/b00w8vzm.jpg?nodefault=true" 
//                alt="Image for 28/11/2010" height="144" width="256"> 
//            <span class="message">
//                <span class="text">Not currently available</span>
//            </span> 
//        </div>  
//    </div>    
//    <div class="nexton"></div>
//    <div class="programmes-commercial-availability-item">
//        <h3 class="title">Buy online</h3>
//        <p class='available-formats'>On: CD</p>
//        <p class='available-suppliers'>From: Amazon, AudioGO</p>
//        <p class='buy-where'>
//            <a href='/programmes/b00w8vzm/products'>Where to buy</a>
//        </p>
//    </div>
//</li>
//
// Series, not individual episode
//

//<li class="series" id="b00l6g04">
//    <div class="series-item" resource="/programmes/b00l6g04#programme" typeof="po:Series">
//            <div class="summary">
//                <h2 class="title">
//                    <a href="/programmes/b00l6g04" property="dc:title" 
//                    resource="/programmes/b00l6g04#programme">Series 28</a>
//                </h2>
//                <p property="po:short_synopsis">Comedy sketches and satirical comments from Steve Punt, Hugh Dennis and the team
//                </p>
//            </div>
//                <div rel="foaf:depiction" class="depiction">
//                    <img src="http://ichef.bbci.co.uk/images/ic/256x144/legacy/series/b00l6g04.jpg?nodefault=true" 
//                    alt="Image for Series 28" height="144" width="256">
//            </div>
//            <div class="programmes-commercial-availability-item">
//            <h3 class="title">Buy online</h3>
//            <p class='available-formats'>On: Audio download, CD</p>
//            <p class='available-suppliers'>From: Amazon, AudioGO</p>
//            <p class='buy-where'>
//                <a href='/programmes/b00l6g04/products'>Where to buy</a>
//            </p>
//        </div>
//    </div>
//    <div class="series-children"> 
//        <ul class="introduction" id="series-introduction-b00l6g04"> 
//            <li class="episodes"> 
//                <a rel="rdfs:seeAlso" href="/programmes/b00l6g04/episodes/guide">View episodes</a> 
//            </li> 
//            <li class="player"> 
//                <span class="count">Available now 
//                    <span class="number">0</span>
//                </span> 
//            </li> 
//            <li class="nexton"> 
//                <span class="count">Next on 
//                    <span class="number">0</span>
//                </span> 
//            </li> 
//            <li class="repeats">
//                <span class="container">
//                    <span class="copy">Repeats</span>
//                </span></li> 
//            <li class="buy"> 
//                <span class="count">Buy 
//                    <span class="number">0</span>
//                </span> 
//            </li> 
//        </ul> 
//    </div> 
//    <script type="text/javascript"> window.episode_guide_data['b00l6g04'] = { url : '/programmes/b00l6g04/episodes/guide', include_url : '/programmes/b00l6g04/episodes/guide.esi', loaded : false, available : 0, upcoming : 0 } 
//    </script> 
//    <div class="children" id="series-children-b00l6g04"></div>
//</li>

        /// <summary>
        /// Event handler for HtmlWeb.PreRequestHandler. Occurs before an HTTP request is executed.
        /// </summary>
        protected bool OnPreRequest(HttpWebRequest request)
        {
            //AddCookiesTo(request);               // Add cookies that were saved from previous requests
            //if (_isPost) AddPostDataTo(request); // We only need to add post data on a POST request
            return true;
        }

       

        /// <summary>
        /// Event handler for HtmlWeb.PostResponseHandler. Occurs after a HTTP response is received
        /// </summary>
        protected void OnAfterResponse(HttpWebRequest request, HttpWebResponse response)
        {

            HttpStatusCode code = response.StatusCode;
            Uri originalRequest = request.RequestUri;
            returnCodes.Add(originalRequest, code);

            //SaveCookiesFrom(response); // Save cookies for subsequent requests
        }

        /// <summary>
        /// Event handler for HtmlWeb.PreHandleDocumentHandler. Occurs before a HTML document is handled
        /// </summary>
        protected void OnPreHandleDocument(HtmlDocument document)
        {
            //SaveHtmlDocument(document); // unecessary comment
        }

        private int retrieveProgrammeInformation(string programmeID)
        {
            // http://www.bbc.co.uk/programmes/b03c46nt.rdf
            // http://feeds.bbc.co.uk/iplayer/episode/b03c46nt
            // http://www.bbc.co.uk/iplayer/playlist/b03c46nt
            // http://www.bbc.co.uk/mediaselector/4/mtis/stream/b03c46k0?cb=30667
            // http://www.bbc.co.uk/mediaselector/4/mtis/stream/b03c46k0/iplayer_intl_stream_aac_rtmp_concrete/limelight?cb=72582
            // http://www.bbc.co.uk/programmes/b03c46k0.rdf

            string url = @"http://www.bbc.co.uk/programmes/" + programmeID + ".rdf";

            IGraph g = new Graph();
            UriLoader.Load(g, new Uri(url));

            foreach (Triple t in g.Triples)
            {
                string a = t.Subject.ToString();
                string b = t.Predicate.ToString();
                string c = t.Object.ToString();

                Console.WriteLine(a + " : " + b + " : " + c);
            }

            //Assuming we have some Graph g find all the URI Nodes
            Console.WriteLine("URI"); 
            foreach (IUriNode u in g.Nodes.UriNodes())
            {            
                Console.WriteLine(u.Uri.ToString());
            }

            //Assuming we have some Graph g find all the URI Nodes
            Console.WriteLine("Literal"); 
            foreach (ILiteralNode u in g.Nodes.LiteralNodes())
            {                
                Console.WriteLine(u.ToString());
            }

            //Assuming we have some Graph g find all the URI Nodes
            Console.WriteLine("Variable"); 
            foreach (IVariableNode u in g.Nodes.VariableNodes())
            {
                Console.WriteLine(u.ToString());
            }

            //Assuming we have some Graph g find all the URI Nodes
            Console.WriteLine("GraphLiteral"); 
            foreach (IGraphLiteralNode u in g.Nodes.GraphLiteralNodes())
            {     
                Console.WriteLine(u.ToString());
            }

            HtmlAgilityPack.HtmlDocument doc;
            HtmlAgilityPack.HtmlWeb web = new HtmlWeb();

            web.PreRequest = new HtmlWeb.PreRequestHandler(OnPreRequest);
            web.PostResponse = new HtmlWeb.PostResponseHandler(OnAfterResponse);
            web.PreHandleDocument = new HtmlWeb.PreHandleDocumentHandler(OnPreHandleDocument);

            Uri sentURI = new Uri(url);

            try
            {
                doc = web.Load(url);

            }
            catch (WebException ex)
            {
                Debug.WriteLine("weberror: " + ex.Data + "\n"); 
                return 0;
            }

            catch (Exception ex)
            {
                Debug.WriteLine("weberror: " + ex.Data + "\n"); 
                return 0;
            }



            try {

                //string xpath = "//po:short_synopsis";
                //string xpath = "//rdf:RDF";
                var pNodes = doc.DocumentNode.SelectSingleNode("//div[@id='myTrips']");
            }
            catch (Exception ex) {
                Debug.WriteLine("weberror: " + ex.Data + "\n"); 
                return 0;
            }


            return 0;
        }



        // given a page for a programme and a page, retrieve any available episode name and IDs (drilling down into those where possible) 
        // updating the programmes table and the programmes-episode table

        private int retrieveEpisodesForProgrammePerPage(string programName,string programmeID, int pageNumber, List<Tuple<string,string>> foundEpisodeProgrammeIDs)
        {

            Debug.WriteLine("Retrieving episodes from " + programName + " PID=" + programmeID + " page " + pageNumber + "\n");

            MySqlConnection Connection = new MySqlConnection("database=podcasts;server=localhost;uid=root;pwd=sedona");
            //Connection.ConnectionString = "database=podcasts;server=localhost;uid=root;pwd=sedona";
            Connection.Open();

            // ensure it's already in the programmes table

            addProgrammeToDatabaseIfAbsent(Connection,programmeID, programName, false);

            // string savedFile = @"C:\Users\Public\TestFolder\nowshow_" + programmeID + "p" +  pageNumber + ".xml";

            HtmlAgilityPack.HtmlWeb web = new HtmlWeb();
            web.PreRequest = new HtmlWeb.PreRequestHandler(OnPreRequest);
            web.PostResponse = new HtmlWeb.PostResponseHandler(OnAfterResponse);
            web.PreHandleDocument = new HtmlWeb.PreHandleDocumentHandler(OnPreHandleDocument);

            string url;
            
            if (pageNumber == 1)
                 url = @"http://www.bbc.co.uk/programmes/" + programmeID + @"/episodes/guide";
            else url = @"http://www.bbc.co.uk/programmes/" + programmeID + @"/episodes/guide?page=" + pageNumber;

            HtmlAgilityPack.HtmlDocument doc;

            Uri sentURI = new Uri(url);

            try
            {

                 doc = web.Load(url);

            }
            catch (WebException ex)
            {
                Debug.WriteLine("weberror: " + ex.Data + "\n"); 
                return 0;
            }
            
            catch (Exception ex)
            {
                Debug.WriteLine("weberror: " + ex.Data + "\n");  
                return 0;
            }

            if (returnCodes.ContainsKey(sentURI))
            {
                HttpStatusCode returnCode = returnCodes[sentURI];
                if (returnCode == HttpStatusCode.NotFound) {

                    returnCodes.Remove(sentURI);
                    return 0;
                }

                returnCodes.Remove(sentURI);
            }


            string check = doc.ToString();
            // doc.Save(savedFile);

            // find highest episode

            int highPageCount = 1;

            if (pageNumber == 1)
                for (int thisPage = 2; thisPage < 100; thisPage++)
                {

                    string xpath = "//a[@href='/programmes/" + programmeID + "/episodes/guide?page=" + thisPage + "']";

                    HtmlNode pageLinkNode = doc.DocumentNode.SelectSingleNode(xpath);

                    if (pageLinkNode == null)
                        break;

                    highPageCount = thisPage;

                }

           int totalCount;

           // check for page not found, ie this prog/episode has no futher sub-episodes
 
           // <meta property="og:title" content=" BBC -  Programmes - Page not found " />
                       
           HtmlNode check404 = doc.DocumentNode.SelectSingleNode("//meta[@content=' BBC -  Programmes - Page not found ']");

           if (check404 == null)
           {

               MySqlCommand comm = new MySqlCommand();
               comm.Connection = Connection;

               //string ConnectionString = "database=podcasts;server=localhost;uid=root;pwd=sedona";
               //MySqlConnection Connection = new MySqlConnection(ConnectionString);
               //Connection.ConnectionString = "database=podcasts;server=localhost;uid=root;pwd=sedona";

               try
               {
                   //Connection.Open();

                   // work through the episodes

                   foreach (HtmlNode nextEpisodeNode in doc.DocumentNode.SelectNodes("//li[@class='series' or @class='episode' or @class='episode alt']"))
                   {

                       string class_value1 = nextEpisodeNode.Attributes["class"].Value;

                       if (class_value1 == "series")
                       {

                           string episodeID = nextEpisodeNode.Attributes["id"].Value;

                           HtmlNode seriesItemNode = nextEpisodeNode.SelectSingleNode("./div[@class='series-item']");
                           string seriesItemNode_resource = seriesItemNode.Attributes["resource"].Value;       // /programmes/b01sjjg0#programme
                           string seriesItemNode_typeof_value = seriesItemNode.Attributes["typeof"].Value;     // po:Series

                           HtmlNode titleNode = seriesItemNode.SelectSingleNode("./div[@class='summary']/h2/a");

                           string titleNode_href = titleNode.Attributes["href"].Value;                         // /programmes/b01sjjg0
                           string titleNode_property = titleNode.Attributes["property"].Value;                 // dc:title
                           string titleNode_resource = titleNode.Attributes["resource"].Value;                 // /programmes/b01sjjg0#programme
                           string titleNode_episodeName = titleNode.InnerText;                                 // Series 40

                           HtmlNode imageNode = seriesItemNode.SelectSingleNode("./div[@class='depiction']/img");
                           string imageNode_src = imageNode.Attributes["src"].Value;                           // http://ichef.bbci.co.uk/images/ic/256x144/legacy/series/b01sjjg0.jpg?nodefault=true
                           string imageNode_alt = imageNode.Attributes["alt"].Value;                           // Image for Series 40
                           string imageNode_height = imageNode.Attributes["height"].Value;                     // 144
                           string imageNode_width = imageNode.Attributes["width"].Value;                       // 256

                           addProgrammeToDatabaseIfAbsent(Connection, episodeID, titleNode_episodeName, false);

                           // add new programme for this series

                           // record it for later

                           foundEpisodeProgrammeIDs.Add(Tuple.Create(episodeID, titleNode_episodeName));

                           // now need to update the programme to episode lookup table

                           // see if the entry exists already in the programme-episode lookup

                           string programmeToEpisodeLinkExistsCheck = "SELECT count(*) FROM programme_episode_lookup WHERE programmeID=@programmeId AND episodeID=@episodeID";
                           comm.CommandText = programmeToEpisodeLinkExistsCheck;
                           comm.Parameters.Clear();

                           comm.Parameters.AddWithValue("@episodeID", episodeID);
                           comm.Parameters.AddWithValue("@programmeID", programmeID);
                           totalCount = Convert.ToInt32(comm.ExecuteScalar());

                           if (totalCount == 0)
                           {

                               // not there yet: add it in

                               comm.CommandText = ("INSERT programme_episode_lookup (programmeID, episodeID) VALUES (@programmeID, @episodeID)");
                               comm.ExecuteNonQuery();
                           }

                       }

                       else if ((class_value1 == "episode") || (class_value1 == "episode alt"))
                       {

                           // string episodeID = nextEpisodeNode.Attributes["id"].Value;
    //<div class="episode-item" resource="/programmes/b0103rz0#programme" typeof="po:Episode"> 
    //    <div class="summary">  
    //        <h4> 
    //            <a href="/programmes/b0103rz0" about="/programmes/b0103rz0#programme"> 
    //                <span class="title" property="dc:title">01/04/2011</span></a> 
    //        </h4>  
    //        <p class="synopsis">  
    //            <span property="po:short_synopsis">Fresh from 4 - Steve Punt and Hugh Dennis present a satirical look at the 
    //            week's news.</span> 
    //        </p>   
    //        <p class="first-broadcast"> 
    //            <a href="/programmes/b0103rz0#programme-broadcasts">First broadcast</a>: 08 Apr 2011 
    //        </p>  
    //    </div> 
    //    <div rel="foaf:depiction" class="depiction"> 
    //        <img src="http://ichef.bbci.co.uk/images/ic/256x144/legacy/episode/b0103rz0.jpg?nodefault=true" 
    //            alt="Image for 01/04/2011" height="144" width="256" /> 
    //        <span class="message">
    //        <span class="text">Not currently available</span>
    //        </span> 
    //    </div>  
    //</div> 
                            HtmlNode episodeItemNode = nextEpisodeNode.SelectSingleNode("./div[@class='episode-item']");
                            string episodeItemNode_resource = episodeItemNode.Attributes["resource"].Value;       // /programmes/b01sjjg0#programme
                            string episodeItemNode_typeof_value = episodeItemNode.Attributes["typeof"].Value;     // po:Episode

                            HtmlNode titleNode = episodeItemNode.SelectSingleNode("./div[@class='summary']/h4/a");

                            string titleNode_href = titleNode.Attributes["href"].Value;                         // /programmes/b00w8vzm
                            string titleNode_about = titleNode.Attributes["about"].Value;                       // /programmes/b00w8vzm#programm
                            string titleNode_episodeName = titleNode.InnerText;                                 // 30/3/2012

                            string episodeID = titleNode_href.Substring(12, 8);

                            HtmlNode synopsisNode = episodeItemNode.SelectSingleNode("./div[@class='summary']/p[@class='synopsis']/span");

                            string synopsisNode_property = synopsisNode.Attributes["property"].Value;           // po:short_synopsis
                            string synopsisNode_shortSynopsis = synopsisNode.InnerText;                          // Steve Punt and Hugh Dennis take a satirical look at the week's news from 19 November 2010

                            HtmlNode firstBroadcastNode = episodeItemNode.SelectSingleNode("./div[@class='summary']/p[@class='first-broadcast']");
                            string firstBroadcastNode_firstBroadcast = firstBroadcastNode.InnerText;

                            HtmlNode imageNode = episodeItemNode.SelectSingleNode("./div[@class='depiction']/img");
                            string imageNode_src = imageNode.Attributes["src"].Value;                           // http://ichef.bbci.co.uk/images/ic/256x144/legacy/series/b01sjjg0.jpg?nodefault=true
                            string imageNode_alt = imageNode.Attributes["alt"].Value;                           // Image for Series 40
                            string imageNode_height = imageNode.Attributes["height"].Value;                     // 144
                            string imageNode_width = imageNode.Attributes["width"].Value;                       // 256

                            //addProgrammeToDatabaseIfAbsent(Connection, episodeID, titleNode_episodeName, false);
                            addProgrammeToDatabaseIfAbsent(Connection, episodeID, titleNode_episodeName, firstBroadcastNode_firstBroadcast, imageNode_src,synopsisNode_shortSynopsis, false);

                            // add new programme for this episode

                            foundEpisodeProgrammeIDs.Add(Tuple.Create(episodeID, titleNode_episodeName));

                            // now need to update the programme to episode lookup table

                            // see if the entry exists already in the programme-episode lookup

                            string programmeToEpisodeLinkExistsCheck = "SELECT count(*) FROM programme_episode_lookup WHERE programmeID=@programmeId AND episodeID=@episodeID";
                            comm.CommandText = programmeToEpisodeLinkExistsCheck;
                            comm.Parameters.Clear();

                            comm.Parameters.AddWithValue("@episodeID", episodeID);
                            comm.Parameters.AddWithValue("@programmeID", programmeID);
                            totalCount = Convert.ToInt32(comm.ExecuteScalar());

                            if (totalCount == 0)
                            {

                                // not there yet: add it in

                                comm.CommandText = ("INSERT programme_episode_lookup (programmeID, episodeID) VALUES (@programmeID, @episodeID)");
                                comm.ExecuteNonQuery();
                            }

                        }




                   }

                   Connection.Close();
               }

               catch (MySqlException ex)
               {
                   Debug.WriteLine("SQLerror: " + ex.Data + "\n"); 
               }
            } 

            return highPageCount;


        }

        private void addProgrammeToDatabaseIfAbsent(MySqlConnection Connection, string programmeID, string title, string firstBroadcast, string imagesrc, string shortSynopsis, Boolean isProgrammeRoot)
        {


            MySqlCommand comm = new MySqlCommand();

            //string ConnectionString = "database=podcasts;server=localhost;uid=root;pwd=sedona";
            //MySqlConnection Connection = new MySqlConnection(ConnectionString);
            //Connection.ConnectionString = "database=podcasts;server=localhost;uid=root;pwd=sedona";

            try
            {
                //Connection.Open();

                comm.Connection = Connection;

                string programmeExistsInProgrammesCheck = "SELECT count(*) FROM programmes WHERE programmeID = @programmeId";

                comm.CommandText = programmeExistsInProgrammesCheck;
                comm.CommandType = CommandType.Text;

                comm.Parameters.Clear();

                comm.Parameters.AddWithValue("@programmeId", programmeID);

                int totalCount = Convert.ToInt32(comm.ExecuteScalar());

                if (totalCount == 0)
                {

                    Debug.WriteLine("..adding " + title + " PID=" + programmeID + "\n");

                    // this episodeColumn does not have a programme in the programme table, so add it

                    MySqlCommand cmd = new MySqlCommand();
                    cmd.Connection = Connection;
                    cmd.CommandText = ("INSERT programmes (programmeID, title,isProgrammeRoot,short_synopsis) VALUES (@programmeID, @title, @isProgrammeRoot,@short_synopsis)");
                    cmd.Parameters.AddWithValue("@programmeID", programmeID);
                    cmd.Parameters.AddWithValue("@title", title);
                    cmd.Parameters.AddWithValue("@isProgrammeRoot", isProgrammeRoot);
                    cmd.Parameters.AddWithValue("@short_synopsis", shortSynopsis);
                    cmd.ExecuteNonQuery();
                }

                //Connection.Close();
            }

            catch (MySqlException ex)
            {
                Debug.WriteLine("SQLerror: " + ex.Data + "\n");
                //Connection.Close();
            }
        }


        private void addProgrammeToDatabaseIfAbsent(MySqlConnection Connection,string programmeID,string title,Boolean isProgrammeRoot)
        {

            MySqlCommand comm = new MySqlCommand();

            //string ConnectionString = "database=podcasts;server=localhost;uid=root;pwd=sedona";
            //MySqlConnection Connection = new MySqlConnection(ConnectionString);
            //Connection.ConnectionString = "database=podcasts;server=localhost;uid=root;pwd=sedona";

            try
            {
                //Connection.Open();

                comm.Connection = Connection;

                string programmeExistsInProgrammesCheck = "SELECT count(*) FROM programmes WHERE programmeID = @programmeId";

                comm.CommandText = programmeExistsInProgrammesCheck;
                comm.CommandType = CommandType.Text;

                comm.Parameters.Clear();

                comm.Parameters.AddWithValue("@programmeId", programmeID);
                int totalCount = Convert.ToInt32(comm.ExecuteScalar());

                if (totalCount == 0)
                {

                    Debug.WriteLine("..adding " + title + " PID=" + programmeID + "\n");

                    // this episodeColumn does not have a programme in the programme table, so add it

                    MySqlCommand cmd = new MySqlCommand();
                    cmd.Connection = Connection;
                    cmd.CommandText = ("INSERT programmes (programmeID, title,isProgrammeRoot) VALUES (@programmeID, @title, @isProgrammeRoot)");
                    cmd.Parameters.AddWithValue("@programmeID", programmeID);
                    cmd.Parameters.AddWithValue("@title", title);
                    cmd.Parameters.AddWithValue("@isProgrammeRoot", isProgrammeRoot);
                    cmd.ExecuteNonQuery();
                }
  
                //Connection.Close();
            }

           catch (MySqlException ex)
            {
               Debug.WriteLine("SQLerror: " + ex.Data + "\n");
               //Connection.Close();
            }

        }




        // Retrieve all the various genres, organised by genre

        private void GenresProgramsButton_Click(object sender, RoutedEventArgs e)
        {

            // <p class="note">Pick a genre to see BBC Radio programmes</p>
            //<ol class="categories" rel="skos:narrower">
            //<li class="genre" typeof="po:Genre" about="/radio/programmes/genres/childrens#genre">             // genre=childrens
            //<a resource="/radio/programmes/genres/childrens#genre" href="/radio/programmes/genres/childrens">
            // <span property="rdfs:label" class="genre-title">Children's</span>
            // </a>
            //<ol class="categories" rel="skos:narrower">
            //<li class="genre" typeof="po:Genre" about="/radio/programmes/genres/childrens/drama#genre">       // subgenre=drama
            //<a resource="/radio/programmes/genres/childrens/drama#genre" href="/radio/programmes/genres/childrens/drama">
            // <span property="rdfs:label" class="genre-title">Drama</span>
            // </a>
            // </li>
            //<li class="genre" typeof="po:Genre" about="/radio/programmes/genres/childrens/entertainmentandcomedy#genre"> // subgenre=entertainmentandcomedy
            //<a resource="/radio/programmes/genres/childrens/entertainmentandcomedy#genre" href="/radio/programmes/genres/childrens/entertainmentandcomedy">
            // <span property="rdfs:label" class="genre-title">Entertainment & Comedy</span>
            // </a>
            //<li class="genre" typeof="po:Genre" about="/radio/programmes/genres/childrens/music#genre">
            //<a resource="/radio/programmes/genres/childrens/music#genre" href="/radio/programmes/genres/childrens/music">
            // <span property="rdfs:label" class="genre-title">Music</span>
            // </a>
            // </li>
            // </ol>
            // </li>
            //<li class="genre" typeof="po:Genre" about="/radio/programmes/genres/comedy#genre">
            //<a resource="/radio/programmes/genres/comedy#genre" href="/radio/programmes/genres/comedy">
            // <span property="rdfs:label" class="genre-title">Comedy</span>
            // </a>
            //<ol class="categories" rel="skos:narrower">
            //<li class="genre" typeof="po:Genre" about="/radio/programmes/genres/comedy/character#genre">
            //<a resource="/radio/programmes/genres/comedy/character#genre" href="/radio/programmes/genres/comedy/character">
            // <span property="rdfs:label" class="genre-title">Character</span>
            // </a>
            // </li>

            CategoryList.Clear();
            int genreCount = 0;
            string whereami;

            bool getFile = true;

            string savedFile = @"C:\Users\Public\TestFolder\genres.xml";

            String content;

            if (getFile)
            {
                string url = @"http://www.bbc.co.uk/radio/programmes/genres";
                System.Net.WebClient webclient = new WebClient();
                content = webclient.DownloadString(url);

                TextReader sr = new StringReader(content);
                XmlDocument cleanXML = FromHtml(sr);
                cleanXML.Save(savedFile);

            }

            else
            {
                content = System.IO.File.ReadAllText(savedFile);
            }


            //TextReader sr = new StringReader(content);
            //XmlDocument cleanXML = FromHtml(sr);
            //cleanXML.Save(@"C:\Users\Public\TestFolder\genres.txt");
            //content = cleanXML.ToString();
            //String content = System.IO.File.ReadAllText(@"C:\Users\Public\TestFolder\genres.txt");

            int startSearchPoint= 0;

            int stringStart,stringEnd,length,categoryLength;

            // move to start of categories
            startSearchPoint = content.IndexOf("class=\"categories\"", startSearchPoint);
            // move to start of underlying 'genre' line
            startSearchPoint = content.IndexOf("<li", startSearchPoint);

            int EndOfGenres = content.IndexOf("/div", startSearchPoint);

            using (MySqlCommand comm = new MySqlCommand())
            {

                string ConnectionString = "database=podcasts;server=localhost;uid=root;pwd=sedona";
                MySqlConnection Connection = new MySqlConnection(ConnectionString);

                string genreExistsCheck = "SELECT count(*) FROM genres WHERE genre = @genre AND subgenre=@subgenre";

                comm.Connection = Connection;
                comm.CommandText = genreExistsCheck;
                comm.CommandType = CommandType.Text;

                try
                {
                    Connection.Open();

                    for (; ; )
                    {
                        // look for next genre

                        stringStart = content.IndexOf("/programmes/genres", startSearchPoint) + 19;

                        if (EndOfGenres < stringStart)
                            break;

                        whereami = content.Substring(stringStart, 10);

                        startSearchPoint = stringStart;
                        stringEnd = content.IndexOf("#", startSearchPoint);
                        length = stringEnd - stringStart;
                        string genre = content.Substring(stringStart, length);
                        startSearchPoint = stringStart + length;
                        categoryLength = length;

                        stringStart = content.IndexOf("class=\"category-title\">", startSearchPoint) + 23;
                        stringEnd = content.IndexOf("</span>", stringStart);
                        length = stringEnd - stringStart;
                        string genreColloquial = content.Substring(stringStart, length);
                        startSearchPoint = stringStart + length;

                        // jump span

                        startSearchPoint = content.IndexOf("span", startSearchPoint);

                        // move to next subgenre within genre

                        bool lookingForFirstSubGenre = true;

                        List<Tuple<String, String>> genreList = new List<Tuple<String, String>>();

                        for (; ; )
                        {

                            // first make sure there are any subgenres to be read

                            int categroriesCheck = content.IndexOf("class=\"categories\"", startSearchPoint);
                            int categoryCheck = content.IndexOf("class=\"category\"", startSearchPoint);

                            if ((lookingForFirstSubGenre) && (categroriesCheck > categoryCheck))
                                break;

                            lookingForFirstSubGenre = false;

                            // </ol> finishes the genre
                            int nextOL = content.IndexOf("</ol>", startSearchPoint);
                            int nextLI = content.IndexOf("<li", startSearchPoint);

                            if (nextOL < nextLI)
                            {
                                startSearchPoint = nextLI;
                                break;
                            }

                            stringStart = content.IndexOf("resource=\"/radio/programmes/genres", startSearchPoint) + 35 + categoryLength + 1;

                            whereami = content.Substring(stringStart, 10);

                            startSearchPoint = stringStart;
                            stringEnd = content.IndexOf("#", startSearchPoint);
                            length = stringEnd - stringStart;
                            string subgenre = content.Substring(stringStart, length);
                            startSearchPoint = stringStart + length;

                            stringStart = content.IndexOf("class=\"category-title\">", startSearchPoint) + 23;
                            stringEnd = content.IndexOf("</span>", stringStart);
                            length = stringEnd - stringStart;
                            string subgenreColloquial = content.Substring(stringStart, length);
                            startSearchPoint = stringStart + length;

                            //genreList.Add(new Tuple<string, string>(genreTitle, subgenreTitle));
                            genreCount++;

                            comm.Parameters.Clear();

                            genreColloquial = System.Web.HttpUtility.HtmlDecode(genreColloquial);
                            subgenreColloquial = System.Web.HttpUtility.HtmlDecode(subgenreColloquial);

                            comm.Parameters.AddWithValue("@genre", genre);
                            comm.Parameters.AddWithValue("@subgenre", subgenre);
                            int totalCount = Convert.ToInt32(comm.ExecuteScalar());

                            if (totalCount == 0)
                            {

                                MySqlCommand cmd = new MySqlCommand();
                                cmd.Connection = Connection;
                                cmd.Parameters.AddWithValue("@genre", genre);
                                cmd.Parameters.AddWithValue("@subgenre", subgenre);
                                cmd.Parameters.AddWithValue("@genreColloquial", genreColloquial);
                                cmd.Parameters.AddWithValue("@subgenreColloquial", subgenreColloquial);
                                cmd.CommandText = ("INSERT genres (genre, subgenre,genreColloquial,subgenreColloquial) VALUES (@genre, @subgenre,@genreColloquial,@subgenreColloquial)");

                                cmd.ExecuteNonQuery();

                            }
                        }

                        CategoryList.Add(genre, genreList);

                    }

                    Connection.Close();
                }

                catch (MySqlException ex)
                {
                    Debug.WriteLine("SQLerror: " + ex.Data + "\n"); 
                }

            }

            StatusBox.Clear();
            StatusBox.Text = StatusBox.Text.Insert(StatusBox.SelectionStart, "Retrieved " + genreCount + " genres over " + CategoryList.Count() + " categories");

            //<li class="genre" typeof="po:Genre" about="/programmes/genres/childrens#genre">
            //  <a resource="/programmes/genres/childrens#genre" href="/programmes/genres/childrens">
            //    <span property="rdfs:label" class="genre-title">Children's</span></a> 
            //  <ol class="categories" rel="skos:narrower"> 
            //    <li class="genre" typeof="po:Genre" about="/programmes/genres/childrens/activities#genre">
            //      <a resource="/programmes/genres/childrens/activities#genre" href="/programmes/genres/childrens/activities">
            //        <span property="rdfs:label" class="genre-title">Activities</span></a></li>
            //  </ol>

            //<li class="genre" typeof="po:Genre" about="/programmes/genres/comedy#genre">
            //  <a resource="/programmes/genres/comedy#genre" href="/programmes/genres/comedy">
            //    <span property="rdfs:label" class="genre-title">Comedy</span></a>  
            //    <ol class="categories" rel="skos:narrower">
            //      <li class="genre" typeof="po:Genre" about="/programmes/genres/comedy/character#genre">
            //        <a resource="/programmes/genres/comedy/character#genre" href="/programmes/genres/comedy/character">
            //          <span property="rdfs:label" class="genre-title">Character</span></a></li>
            //    </ol>
            //
            //  </div>   (all complete)

        }

        // given a programme ID, establish what episodes are associated with it


        private void establishEpisodes(string programmeID)
        {


            //http://www.bbc.co.uk/programmes/b00srz5b/episodes/guide
            //http://www.bbc.co.uk/programmes/b006qftk/episodes/guide?page=3

            System.Net.WebClient webclient = new WebClient();

            int pageCount = 1;
            int thisPage = 1;

            string content;
            
            String url;

            for (; ; )                    // loop through episode pages for this programme 
            {

                if (thisPage > pageCount)
                    break;

                string savedFile = @"C:\Users\Public\TestFolder\podcasts\episodes\prog__" + programmeID + "_p" + thisPage + ".xml";

                if (!File.Exists(savedFile))
                {
                    if (thisPage == 1)
                         url = @"http://www.bbc.co.uk/" + programmeID + "/episodes/guide";
                    else url = @"http://www.bbc.co.uk/" + programmeID + "/episodes/guide?page=" + thisPage;
                    content = webclient.DownloadString(url);

                    TextReader sr = new StringReader(content);
                    XmlDocument cleanXML = FromHtml(sr);
                    cleanXML.Save(savedFile);
                }

                else
                {
                    content = System.IO.File.ReadAllText(savedFile);
                }

                pageCount = scrapeEpisodes(content);

                thisPage++;
            }
        }

        private int scrapeEpisodes(string content)
        {

            int pageCount = 1;

            System.Net.WebClient webclient = new WebClient();

            return pageCount;
        }


        private void AtoZProgramsButton_Click(object sender, RoutedEventArgs e)
        {
            System.Net.WebClient webclient = new WebClient();

            AZprogramList.Clear();

            for (char letter = 'a'; letter <= 'z'; letter++)
            {

                int pageCount=1;
                int thisPage=1;

                string content;
                string savedFile = @"C:\Users\Public\TestFolder\podcasts\az\a-z_" + letter + "_p" + thisPage + ".xml";
                String url;

                for (;;)                    // loop through pages for this letter 
                {

                    if (thisPage > pageCount)
                        break;

                    if (!File.Exists(savedFile))
                    {
                        if (thisPage == 0)
                            url = @"http://www.bbc.co.uk/radio/programmes/a-z/by/" + letter;
                        else url = @"http://www.bbc.co.uk/radio/programmes/a-z/by/" + letter + "?page=" + thisPage;
                        content = webclient.DownloadString(url);

                        TextReader sr = new StringReader(content);
                        XmlDocument cleanXML = FromHtml(sr);
                        cleanXML.Save(savedFile);
                    }

                    else
                    {
                        content = System.IO.File.ReadAllText(savedFile);
                    }

                    //int startSearchPoint = 0;
                    //int length;
                    //int stringStart, stringEnd;
                    //MySqlCommand cmd;

                    pageCount = ReadAZpage(letter, thisPage, webclient);

                    //for (;;)        // while there are pages to process
                    //{
 
                    //}

                    thisPage++;
                }

                StatusBox.Clear();
                StatusBox.Text = StatusBox.Text.Insert(StatusBox.SelectionStart, "Retrieved " + letter);

                //System.IO.File.WriteAllText(@"C:\Users\Public\TestFolder\APrograms.txt", content);
            }

           // Connection.Close();

        }

  

//<li class="module--innercontent atoz-programmes--item available-programme">
// <div class="atoz-programmes--item-wrap">
//  <a class="programme-object" data-pid="b00srz5b" href="/programmes/b00srz5b">
//   <div class="programme-object--wrapper">
//    <div rel="foaf:depiction" class="programme-object--depiction">
//     <img alt="" src="http://static.bbci.co.uk/programmes/2.58.0/img/blank.png" data-img-src-80="http://ichef.bbci.co.uk/images/ic/160x90/legacy/series/b00srz5b.jpg?nodefault=true" data-img-src-196="http://ichef.bbci.co.uk/images/ic/256x144/legacy/series/b00srz5b.jpg?nodefault=true" data-img-src-320="http://ichef.bbci.co.uk/images/ic/480x270/legacy/series/b00srz5b.jpg?nodefault=true" data-img-src-560="http://ichef.bbci.co.uk/images/ic/640x360/legacy/series/b00srz5b.jpg?nodefault=true" data-img-src-720="http://ichef.bbci.co.uk/images/ic/976x549/legacy/series/b00srz5b.jpg?nodefault=true"/>
//     <div class="programme-object--overlay">
//      <span class="favourites-module-wrapper">
//       <span class="favourites-module">
//        <span class="p-f p-f-v1 p-f-variant-medium p-f-lang-en-gb" data-id="b00srz5b" data-title="A Brief History of Mathematics" data-appid="radio" data-type="tlec" data-item="urn:bbc:radio:tlec:b00srz5b:title=A%20Brief%20History%20of%20Mathematics" data-lang="en-gb" data-variant="medium">
//         <form method="post" action="http://www.bbc.co.uk/modules/personalisation/my/favourites">
//          <input type="hidden" name="item" value="urn:bbc:radio:tlec:b00srz5b:title=A%20Brief%20History%20of%20Mathematics"/>
//          <input type="hidden" name="ptrt" value="/radio/programmes/a-z/by/a"/>
//          <button id="pf3" type="submit" class="p-f-button " aria-labelledby="pfl3" aria-live="polite" title="Add">
//           <span class="p-f-icon">
//            <img src="http://static.bbci.co.uk/modules/plugin/favourite/1.1.4/1_1_4/img/sprite-3.png" class="p-f-icon-sprite" width="128" height="384" alt=""/>
//            <img src="http://static.bbci.co.uk/modules/plugin/favourite/1.1.4/1_1_4/img/spinner.gif" class="p-f-icon-spinner" width="128" height="128" alt=""/>
//           </span>
//           <span id="pfl3" class="p-f-label p-f-hidden" tabindex="-1">Add</span>
//           <span class="p-f-label-display p-f-show" role="presentation" aria-hidden="true">
//            <span class="p-f-label-text">Add</span>
//            <span class="p-f-label-action"/>
//           </span>
//          </button>
//         </form>
//        </span>
//       </span>
//      </span>
//     </div>
//    </div>
//    <div class="programme-object--summary">
//     <div class="programme-object--details">
//      <h4 class="programme-object--titles">
//       <span class="programme-object--title" property="dc:title">A Brief History of Mathematics</span>
//      </h4>
//      <p class="programme-object--synopsis">
//       <span property="po:short_synopsis">Professor Marcus du Sautoy reveals the personalities behind the calculations</span>
//      </p>
//      <p class="programme-object--service"> BBC Radio 4 </p>
//     </div>
//    </div>
//   </div>
//  </a>
//  <p class="iplayer-cta--inline">
//    <a class="iplayer-cta--inline-link" href="/programmes/b00srz5b/episodes/player" data-inc-path="/programmes/b00srz5b/episodes/player.inc">
//    <i class="icon"></i>
//    <span class="iplayer-cta-label">Show available episodes</span>
//   </a>
//  </p>
// </div>
//</li>
        //XmlNodeList nodeList = doc.SelectNodes("//settings/*[starts-with(name(),'name')]");


        int ReadAZpage(char letter, int pagenumber, System.Net.WebClient webclient)
        {

            MySqlCommand cmd;
            string content;
            string savedFile = @"C:\Users\Public\TestFolder\podcasts\az\a-z_" + letter + "_p" + pagenumber + ".xml";
            String url;

            XmlDocument XMLDocin;

            if (!File.Exists(savedFile))
            {
                if (pagenumber == 1)
                    url = @"http://www.bbc.co.uk/radio/programmes/a-z/by/" + letter;
                else url = @"http://www.bbc.co.uk/radio/programmes/a-z/by/" + letter + "?page=" + pagenumber;
                content = webclient.DownloadString(url);

                TextReader sr = new StringReader(content);
                XMLDocin = FromHtml(sr);
                XMLDocin.Save(savedFile);
            }

            else
            {
                XMLDocin = new XmlDocument();
                XMLDocin.PreserveWhitespace = true;
                XMLDocin.XmlResolver = null;
                XMLDocin.Load(savedFile);
            }

           XmlNodeList programmeList = XMLDocin.SelectNodes("//li[@class='module--innercontent atoz-programmes--item available-programme']");


           using (MySqlCommand comm = new MySqlCommand())
           {

               string ConnectionString = "database=podcasts;server=localhost;uid=root;pwd=sedona";
               MySqlConnection Connection = new MySqlConnection(ConnectionString);

               string programmeExistsInProgrammesCheck = "SELECT count(*) FROM programmes WHERE programmeID = @ID";

               comm.Connection = Connection;
               comm.CommandText = programmeExistsInProgrammesCheck;
               comm.CommandType = CommandType.Text;

               try
               {
                   Connection.Open();

                   foreach (XmlNode node in programmeList)
                   {

                       ProgramEpisode nextEpisode = new ProgramEpisode();

                       XmlNode programmeNode = node.SelectSingleNode("div/a");

                       nextEpisode.progID = programmeNode.Attributes["data-pid"].Value;
                       Debug.WriteLine("programmeID=" + nextEpisode.progID + "\n");

                       // XmlNode imgNode = node.SelectSingleNode(#//dic[@class='module--innercontent atoz-programmes--item available-programme']");
                       XmlNode depictionDivNode = node.SelectSingleNode("div/a/div/div[@class='programme-object--depiction']");
                       //XmlNode baseDivNode = node.SelectSingleNode("./div");
                       XmlNode imgNode = depictionDivNode.SelectSingleNode("img");

                       nextEpisode.imageURL80 = imgNode.Attributes["data-img-src-80"].Value;
                       nextEpisode.imageURL196 = imgNode.Attributes["data-img-src-196"].Value;
                       nextEpisode.imageURL320 = imgNode.Attributes["data-img-src-320"].Value;
                       nextEpisode.imageURL560 = imgNode.Attributes["data-img-src-560"].Value;
                       nextEpisode.imageURL720 = imgNode.Attributes["data-img-src-720"].Value;

                       XmlNode depictionOverlayNode = depictionDivNode.SelectSingleNode("div[@class='programme-object--overlay']");

                       XmlNode detailsNode = depictionOverlayNode.SelectSingleNode("span/span/span");

                       nextEpisode.data_id = detailsNode.Attributes["data-id"].Value;
                       nextEpisode.data_title = detailsNode.Attributes["data-title"].Value;
                       nextEpisode.data_appid = detailsNode.Attributes["data-appid"].Value;
                       nextEpisode.data_type = detailsNode.Attributes["data-type"].Value;
                       nextEpisode.data_item = detailsNode.Attributes["data-item"].Value;
                       nextEpisode.data_lang = detailsNode.Attributes["data-lang"].Value;
                       nextEpisode.data_variant = detailsNode.Attributes["data-variant"].Value;

                       XmlNode depictionSummaryNode = depictionDivNode.SelectSingleNode("../div[@class='programme-object--summary']");
                       XmlNode summaryTitleNode = depictionSummaryNode.SelectSingleNode("div/h4");
                       XmlNode summarySynopsisNode = depictionSummaryNode.SelectSingleNode("div/p[@class='programme-object--synopsis']/span");
                       nextEpisode.short_synopsis = summarySynopsisNode.InnerText;

                       XmlNode summaryServiceNode = depictionSummaryNode.SelectSingleNode("div/p[@class='programme-object--service']");
                       nextEpisode.service = summaryServiceNode.InnerText;

                       int IDindex = comm.Parameters.IndexOf("@ID");

                       if (IDindex != -1)
                           comm.Parameters.RemoveAt(IDindex);
                       comm.Parameters.AddWithValue("@ID", nextEpisode.progID);     // know this ID already?
                       int totalCount = Convert.ToInt32(comm.ExecuteScalar());

                       if (totalCount == 0)
                       {

                           cmd = new MySqlCommand();
                           cmd.Connection = Connection;

                           cmd.CommandText = ("INSERT programmes (programmeID, title,isProgrammeRoot,appID,type,item,lang,variant,short_synopsis) VALUES (@programmeID, @title, @isProgrammeRoot, @appID, @type, @item, @lang, @variant, @short_synopsis)");

                           //add our parameters to our command object
                           cmd.Parameters.AddWithValue("@programmeID", nextEpisode.progID);
                           cmd.Parameters.AddWithValue("@title", nextEpisode.data_title);
                           cmd.Parameters.AddWithValue("@isProgrammeRoot", 1);
                           cmd.Parameters.AddWithValue("@appID", nextEpisode.data_appid);
                           cmd.Parameters.AddWithValue("@type", nextEpisode.data_type);
                           cmd.Parameters.AddWithValue("@item", nextEpisode.data_item);
                           cmd.Parameters.AddWithValue("@lang", nextEpisode.data_lang);
                           cmd.Parameters.AddWithValue("@variant", nextEpisode.data_variant);
                           cmd.Parameters.AddWithValue("@short_synopsis", nextEpisode.short_synopsis);

                           // Connection.Open();
                           cmd.ExecuteNonQuery();

                       }


                   }

                   Connection.Close();
               }

               catch (MySqlException ex)
               {
                   Debug.WriteLine("SQLerror: " + ex.Data + "\n"); 
               }
           }

           return 0;

        }

   
 

        XmlDocument FromHtml(TextReader reader)
        {

            // setup SgmlReader
            Sgml.SgmlReader sgmlReader = new Sgml.SgmlReader();
            sgmlReader.DocType = "HTML";
            sgmlReader.WhitespaceHandling = WhitespaceHandling.All;
            sgmlReader.CaseFolding = Sgml.CaseFolding.ToLower;
            sgmlReader.InputStream = reader;

            // create document
            XmlDocument doc = new XmlDocument();
            doc.PreserveWhitespace = true;
            doc.XmlResolver = null;
            doc.Load(sgmlReader);
            return doc;
        }


        private void FileSaveButton_Click(object sender, RoutedEventArgs e)
        {

            System.Net.WebClient webclient = new WebClient();

            for (char letter = 'a'; letter <= 'z'; letter++)
            {

                string content;
                string savedFile = @"C:\Users\Public\TestFolder\" + letter + "-" + DateTime.Now.ToString("dd_MM_yyyy") + ".xml";

                if (!File.Exists(savedFile))
                {
                    String url = @"http://www.bbc.co.uk/radio/programmes/a-z/by/" + letter;
                    content = webclient.DownloadString(url);

                    TextReader sr = new StringReader(content);
                    XmlDocument cleanXML = FromHtml(sr);
                    cleanXML.Save(savedFile);
                }

            }


        }

        public void logCallback(string inward)
        {

            lock (this)
            {

                loglist.Add(new LogLine() { thisLine = inward });

                using (StreamWriter w = File.AppendText(@"C:\Users\phil\Documents\Visual Studio 2012\Projects\GetWrapper\GetWrapper\log.txt"))
                {
                    w.WriteLine("{0} {1} {2}", DateTime.Now.ToLongTimeString(),
                        DateTime.Now.ToLongDateString(), inward);
                    //w.WriteLine("  :");
                    //w.WriteLine("  :{0}", inward);
                }
            }
        }


    }

    public class LogLine
    {

        private string _thisline;
        private DateTime _timestamp;

        public string thisLine
        {
            get { return _thisline; }
            set
            {
                _thisline = value;
                timestamp = DateTime.Now;
            }
        }

        public DateTime timestamp
        {
            get { return _timestamp; }
            set { _timestamp = value; }
        }

    }

    public class ProgramEpisode
    {
        public string progID;
        public string data_id;
        public string data_title;
        public string data_appid;
        public string data_type;
        public string data_item;
        public string data_lang;
        public string data_variant;
        public string short_synopsis;
        public string service;

        public string imageURL80;
        public string imageURL196;
        public string imageURL320;
        public string imageURL560;
        public string imageURL720;

        //DateTime broadcast;
        //DateTime updateTime;
        //string passionSite;

    };

}


      //int ReadAZpage_old(char letter, int pagenumber, System.Net.WebClient webclient)
      //  {

      //      AZ_xml_decode();
      //      return 0;


      //      int startSearchPoint = 0;
      //      int length;
      //      int stringStart, stringEnd;
      //      MySqlCommand cmd;
      //      int pageCount;

      //      string content;
      //      string savedFile = @"C:\Users\Public\TestFolder\podcasts\az\a-z_" + letter + "_p" + pagenumber + ".xml";
      //      String url;

      //      if (!File.Exists(savedFile))
      //      {
      //          if (pagenumber == 1)
      //              url = @"http://www.bbc.co.uk/radio/programmes/a-z/by/" + letter;
      //          else url = @"http://www.bbc.co.uk/radio/programmes/a-z/by/" + letter + "?page=" + pagenumber;
      //          content = webclient.DownloadString(url);

      //          TextReader sr = new StringReader(content);
      //          XmlDocument cleanXML = FromHtml(sr);
      //          cleanXML.Save(savedFile);
      //      }

      //      else
      //      {
      //          content = System.IO.File.ReadAllText(savedFile);
      //      }

      //      // keep searching for  <li class="module--innercontent atoz-programmes--item available-programme">
      //      // to indicate all done
      //      //startSearchPoint = content.IndexOf(@"module--innercontent atoz-programmes--item available-programme", startSearchPoint);
      //      //if (startSearchPoint == -1)
      //      //{

      //      //    if (pagenumber == 0)
      //      //    {
      //      //        // find out how many pages we have to retrieve

      //      //        stringStart = content.IndexOf(@"Page " + pagenumber " of ", 0);

      //      //        if (stringStart == -1)
      //      //        {
      //      //            // only one page
      //      //            break;
      //      //        }

      //      //        stringStart += 10;
      //      //        startSearchPoint = stringStart;
      //      //        stringEnd = content.IndexOf("\"", startSearchPoint);
      //      //        length = stringEnd - stringStart;
      //      //        pageCount = Convert.ToInt32(content.Substring(stringStart, length));
      //      //    }

      //      //}

      //      for (;;)
      //      {

      //          startSearchPoint = content.IndexOf(@"module--innercontent atoz-programmes--item available-programme", startSearchPoint);
      //          if (startSearchPoint == -1)
      //              break;

      //          ProgramEpisode nextEpisode = new ProgramEpisode();

      //          startSearchPoint++;

      //          stringStart = content.IndexOf(@"/programmes/", startSearchPoint) + 12;
      //          startSearchPoint = stringStart;
      //          stringEnd = content.IndexOf(@">", startSearchPoint);
      //          length = stringEnd - stringStart - 1;
      //          nextEpisode.progID = content.Substring(stringStart, length);
      //          startSearchPoint = stringStart + length;

      //          stringStart = content.IndexOf(@"data-id", startSearchPoint) + 9;
      //          startSearchPoint = stringStart;
      //          stringEnd = content.IndexOf("\"", startSearchPoint);
      //          length = stringEnd - stringStart;
      //          nextEpisode.data_id = content.Substring(stringStart, length);
      //          startSearchPoint = stringStart + length;

      //          nextEpisode.imageURL80 = @"http://ichef.bbci.co.uk/images/ic/160x90/legacy/episode/" + nextEpisode.data_id + ".jpg";
      //          nextEpisode.imageURL196 = @"http://ichef.bbci.co.uk/images/ic/256x144/legacy/episode/" + nextEpisode.data_id + ".jpg";
      //          nextEpisode.imageURL320 = @"http://ichef.bbci.co.uk/images/ic/480x270/legacy/episode/" + nextEpisode.data_id + ".jpg";
      //          nextEpisode.imageURL560 = @"http://ichef.bbci.co.uk/images/ic/640x360/legacy/episode/" + nextEpisode.data_id + ".jpg";
      //          nextEpisode.imageURL720 = @"http://ichef.bbci.co.uk/images/ic/976x549/legacy/episode/" + nextEpisode.data_id + ".jpg";

      //          stringStart = content.IndexOf(@"data-title", startSearchPoint) + 12;
      //          startSearchPoint = stringStart;
      //          stringEnd = content.IndexOf("\"", startSearchPoint);
      //          length = stringEnd - stringStart;
      //          nextEpisode.data_title = content.Substring(stringStart, length);
      //          startSearchPoint = stringStart + length;

      //          stringStart = content.IndexOf(@"data-appid", startSearchPoint) + 12;
      //          startSearchPoint = stringStart;
      //          stringEnd = content.IndexOf("\"", startSearchPoint);
      //          length = stringEnd - stringStart;
      //          nextEpisode.data_appid = content.Substring(stringStart, length);
      //          startSearchPoint = stringStart + length;

      //          stringStart = content.IndexOf(@"data-type", startSearchPoint) + 11;
      //          startSearchPoint = stringStart;
      //          stringEnd = content.IndexOf("\"", startSearchPoint);
      //          length = stringEnd - stringStart;
      //          nextEpisode.data_type = content.Substring(stringStart, length);
      //          startSearchPoint = stringStart + length;

      //          stringStart = content.IndexOf(@"data-item", startSearchPoint) + 11;
      //          startSearchPoint = stringStart;
      //          stringEnd = content.IndexOf("\"", startSearchPoint);
      //          length = stringEnd - stringStart;
      //          nextEpisode.data_item = content.Substring(stringStart, length);
      //          startSearchPoint = stringStart + length;

      //          stringStart = content.IndexOf(@"data-lang", startSearchPoint) + 11;
      //          startSearchPoint = stringStart;
      //          stringEnd = content.IndexOf("\"", startSearchPoint);
      //          length = stringEnd - stringStart;
      //          nextEpisode.data_lang = content.Substring(stringStart, length);
      //          startSearchPoint = stringStart + length;

      //          stringStart = content.IndexOf(@"data-variant", startSearchPoint) + 14;
      //          startSearchPoint = stringStart;
      //          stringEnd = content.IndexOf("\"", startSearchPoint);
      //          length = stringEnd - stringStart;
      //          nextEpisode.data_variant = content.Substring(stringStart, length);
      //          startSearchPoint = stringStart + length;

      //          stringStart = content.IndexOf(@"short_synopsis", startSearchPoint) + 16;
      //          startSearchPoint = stringStart;
      //          stringEnd = content.IndexOf("<", startSearchPoint);
      //          length = stringEnd - stringStart;
      //          nextEpisode.short_synopsis = content.Substring(stringStart, length);
      //          startSearchPoint = stringStart + length;

      //          stringStart = content.IndexOf(@"programme-object--service", startSearchPoint) + 27;
      //          startSearchPoint = stringStart;
      //          stringEnd = content.IndexOf("<", startSearchPoint);
      //          length = stringEnd - stringStart;
      //          nextEpisode.service = content.Substring(stringStart, length);
      //          startSearchPoint = stringStart + length;

      //          AZprogramList.Add(nextEpisode);

      //          string ConnectionString = "database=podcasts;server=localhost;uid=root;pwd=sedona";
      //          MySqlConnection Connection = new MySqlConnection(ConnectionString);

      //          Debug.WriteLine("programmeID=" + nextEpisode.progID + "\n");

      //          string programmeExistsInProgrammesCheck = "SELECT count(*) FROM programmes WHERE programmeID = @ID";

      //          using (MySqlCommand comm = new MySqlCommand())
      //          {
      //              comm.Connection = Connection;
      //              comm.CommandText = programmeExistsInProgrammesCheck;
      //              comm.CommandType = CommandType.Text;
      //              comm.Parameters.AddWithValue("@ID", nextEpisode.progID);
      //              try
      //              {
      //                  Connection.Open();
      //                  int totalCount = Convert.ToInt32(comm.ExecuteScalar());
      //                  if (totalCount == 0)
      //                  {

      //                      cmd = new MySqlCommand();
      //                      cmd.Connection = Connection;

      //                      cmd.CommandText = ("INSERT programmes (programmeID, title,appID,type,item,lang,variant,short_synopsis) VALUES (@programmeID, @title, @appID, @type, @item, @lang, @variant, @short_synopsis)");

      //                      //add our parameters to our command object
      //                      cmd.Parameters.AddWithValue("@programmeID", nextEpisode.progID);
      //                      cmd.Parameters.AddWithValue("@title", nextEpisode.data_title);
      //                      cmd.Parameters.AddWithValue("@appID", nextEpisode.data_appid);
      //                      cmd.Parameters.AddWithValue("@type", nextEpisode.data_type);
      //                      cmd.Parameters.AddWithValue("@item", nextEpisode.data_item);
      //                      cmd.Parameters.AddWithValue("@lang", nextEpisode.data_lang);
      //                      cmd.Parameters.AddWithValue("@variant", nextEpisode.data_variant);
      //                      cmd.Parameters.AddWithValue("@short_synopsis", nextEpisode.short_synopsis);

      //                      // Connection.Open();
      //                      cmd.ExecuteNonQuery();
      //                      Connection.Close();
      //                      //sds.WriteLine("New Name: " + nextEpisode.progID + " " + totalCount);
      //                  }

      //                  Connection.Close();

      //              }
      //              catch (MySqlException ex)
      //              {

      //                  // error here
      //              }
      //          }
      //      }

      //      // find out how many pages we have to retrieve

      //      if (pagenumber == 1)
      //           stringStart = content.IndexOf(@"Page 2 of ", 0);
      //      else stringStart = content.IndexOf(@"Page " + (pagenumber-1) + " of ", 0);

      //      if (stringStart == -1)
      //      {
      //          // only one page
      //          pageCount = 1;
      //      }

      //      else {

      //          stringStart += 10;
      //          startSearchPoint = stringStart;
      //          stringEnd = content.IndexOf("\"", startSearchPoint);
      //          length = stringEnd - stringStart;
      //          pageCount = Convert.ToInt32(content.Substring(stringStart, length));

      //      }

      //      return pageCount;

      //  }



//private void Window_Loaded_1(object sender, RoutedEventArgs e)
//{

//    //BBCloader.PodcastsDataSet podcastsDataSet = ((BBCloader.PodcastsDataSet)(this.FindResource("podcastsDataSet")));
//    //// Load data into the table genres. You can modify this code as needed.
//    //BBCloader.PodcastsDataSetTableAdapters.genresTableAdapter podcastsDataSetgenresTableAdapter = new BBCloader.PodcastsDataSetTableAdapters.genresTableAdapter();
//    //podcastsDataSetgenresTableAdapter.Fill(podcastsDataSet.genres);
//    //System.Windows.Data.CollectionViewSource genresViewSource = ((System.Windows.Data.CollectionViewSource)(this.FindResource("genresViewSource")));
//    //genresViewSource.View.MoveCurrentToFirst();
//    //// Load data into the table episodes. You can modify this code as needed.
//    //BBCloader.PodcastsDataSetTableAdapters.episodesTableAdapter podcastsDataSetepisodesTableAdapter = new BBCloader.PodcastsDataSetTableAdapters.episodesTableAdapter();
//    //podcastsDataSetepisodesTableAdapter.Fill(podcastsDataSet.episodes);
//    //System.Windows.Data.CollectionViewSource episodesViewSource = ((System.Windows.Data.CollectionViewSource)(this.FindResource("episodesViewSource")));
//    //episodesViewSource.View.MoveCurrentToFirst();
//}

     //void AZ_xml_decode()
     //   {

     //       int startSearchPoint = 0;
     //       int length;
     //       int stringStart, stringEnd;
     //       MySqlCommand cmd;
     //       int pageCount;

     //       string content;
     //       string savedFile = @"C:\Users\Public\TestFolder\podcasts\az\a-z_a_p1.xml";
     //       String url;

     //       if (!File.Exists(savedFile))
     //       {
     //           System.Net.WebClient webclient = new WebClient();
     //           url = @"http://www.bbc.co.uk/radio/programmes/a-z/by/a";
     //           content = webclient.DownloadString(url);

     //           TextReader sr = new StringReader(content);
     //           XmlDocument cleanXML = FromHtml(sr);
     //           cleanXML.Save(savedFile);
     //       }

     //       content = System.IO.File.ReadAllText(savedFile);


     //       // create an XPathDocument object
     //       XPathDocument xmlPathDoc = new XPathDocument(savedFile);

     //       // create a navigator for the xpath doc
     //       XPathNavigator xNav = xmlPathDoc.CreateNavigator();

     //       //navigate and print all the titles
     //       FindNextProgramme(xNav);


     //   }


     //   void AZ_xml_decode2()
     //   {

     //       int startSearchPoint = 0;
     //       int length;
     //       int stringStart, stringEnd;
     //       MySqlCommand cmd;
     //       int pageCount;

     //       string content;
     //       string savedFile = @"C:\Users\Public\TestFolder\podcasts\az\a-z_a_p1.xml";
     //       String url;  

     //       if (!File.Exists(savedFile))
     //       {
     //           System.Net.WebClient webclient = new WebClient();
     //           url = @"http://www.bbc.co.uk/radio/programmes/a-z/by/a";
     //           content = webclient.DownloadString(url);

     //           TextReader sr = new StringReader(content);
     //           XmlDocument cleanXML = FromHtml(sr);
     //           cleanXML.Save(savedFile);
     //       }

     //      content = System.IO.File.ReadAllText(savedFile);


     //       // create an XPathDocument object
     //      XPathDocument xmlPathDoc = new XPathDocument(savedFile);

     //       // create a navigator for the xpath doc
     //       XPathNavigator xNav = xmlPathDoc.CreateNavigator();

     //       //navigate and print all the titles
     //       FindNextProgramme(xNav);


     //   }

//       public static void FindNextProgramme(XPathNavigator p_xPathNav)
//        {

////<div id="atoz-programmes" class="module">
////<ol class="module--content atoz-programmes--list">
////<li class="module--innercontent atoz-programmes--item available-programme">

//            //run the XPath query

//            XPathNodeIterator xPathIt = p_xPathNav.Select("//html/body/div/div/div/div/div/div");

//            //use the XPathNodeIterator to display the results
//            if (xPathIt.Count > 0)
//            {
//                Console.WriteLine("");
//                Console.WriteLine("The catalog contains the following titles:");

//                //begin to loop through the titles and begin to display them
//                while (xPathIt.MoveNext())
//                {
//                    Console.WriteLine(xPathIt.Current.Value);
//                }
//            }
//            else
//            {
//                Console.WriteLine("No titles found in catalog.");
//            }
//        }




//// exists already in Programmes table?

//comm.Connection = Connection;

//string programmeExistsInProgrammesCheck = "SELECT count(*) FROM programmes WHERE programmeID = @programmeId";

//comm.CommandText = programmeExistsInProgrammesCheck;
//comm.CommandType = CommandType.Text;

//comm.Parameters.Clear();

//comm.Parameters.AddWithValue("@programmeId", episodeID);
//totalCount = Convert.ToInt32(comm.ExecuteScalar());

//Debug.WriteLine("..found series " + titleNode_episodeName + " PID=" + episodeID + "\n");

//if (totalCount == 0)
//{

//    // this episodeColumn does not have a programme in the programme table, so add it

//    MySqlCommand cmd = new MySqlCommand();
//    cmd.Connection = Connection;
//    cmd.CommandText = ("INSERT programmes (programmeID, title,isProgrammeRoot) VALUES (@programmeID, @title, @isProgrammeRoot)");
//    cmd.Parameters.AddWithValue("@programmeID", episodeID);
//    cmd.Parameters.AddWithValue("@title", titleNode_episodeName);
//    cmd.Parameters.AddWithValue("@isProgrammeRoot", 0);
//    cmd.ExecuteNonQuery();

//    // record it for later

//    foundEpisodeProgrammeIDs.Add(Tuple.Create(episodeID,titleNode_episodeName));

//    // now need to update the programme to episode lookup table

//    // see if the entry exists already in the programme-episode lookup

//    string programmeToEpisodeLinkExistsCheck = "SELECT count(*) FROM programme_episode_lookup WHERE programmeID=@programmeId AND episodeID=@episodeID";
//    comm.CommandText = programmeToEpisodeLinkExistsCheck;
//    comm.Parameters.Clear();

//    comm.Parameters.AddWithValue("@episodeID", episodeID);
//    comm.Parameters.AddWithValue("@programmeID", programmeID);
//    totalCount = Convert.ToInt32(comm.ExecuteScalar());

//    if (totalCount == 0)
//    {

//        // no there yet: add it in

//        comm.CommandText = ("INSERT programme_episode_lookup (programmeID, episodeID) VALUES (@programmeID, @episodeID)");
//        comm.ExecuteNonQuery();
//    }

//}