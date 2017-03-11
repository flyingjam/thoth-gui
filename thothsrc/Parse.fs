(*
    Does stuff man
*)
module Parse
    open NSoup 
    open Download
    open Monads
    open System

    let ignoredTags (x : NSoup.Nodes.Element) = 
        match (x.TagName()) with
    (*
        Returns false for any tag that is "ignored"
        Used for FindContent
    *)
        |"a" -> false
        |"li" -> false
        |"script" -> false
        |_ -> true

    let lower (x : NSoup.Nodes.Element) (y : NSoup.Nodes.Element) =
        (*
            Tests if the x is lower than y in the html tree by
            seeing if x is in y's children
        *)

        (y.Children) |> Seq.exists (fun z -> z = x)
    
    let GetTitle (document : NSoup.Nodes.Document) =
        (*
            Helper function that wraps around document.Select("title")
            Cannot fail; defaults to "No Title Found"
        *)
        let title = (document.Select("title"))
        match (title |> Seq.isEmpty) with
        |true -> "No Title Found"
        |false -> (title |> Seq.head).Text()

    let FindContent (document : NSoup.Nodes.Document) textThreshold=
        (*
            Given a NSoup document, pulls a list of all elements in the document
            and processes it, finding a selection of tags which likely contain the
            beginning of content in the document
        *)

        let rec loop acc counter fail elements =
            match elements with
            | [] -> Some acc
            | (hd : NSoup.Nodes.Element) :: tl -> 
                    match (hd.OwnText()).Trim() with
                    |_ when fail > 3 -> loop acc 0 0 tl
                    |_ when counter > textThreshold -> loop acc 0 0 []
                    |_ when (hd.OwnText()).Length < 300 -> 
                            loop acc counter (fail + 1) tl
                    |_ when (hd.OwnText()).Length > 300 && ignoredTags hd -> 
                            loop (hd :: acc) (counter + (hd.OwnText()).Length) fail tl
                    |_ -> loop acc counter fail tl

        (document.Body.Select("*")) |> Seq.toList |> loop [] 0 0

    let GetParents (element : NSoup.Nodes.Element) =
        (*
            Gets Some parent or None if null
            Because runtime errors in your functional program whyyy
        *)

        let parent = element.Parent
        if not (parent <> null) then
            None
        else
            Some parent


    let rec ParentByStringContent content =
        (*
            Given a list of potential content tags returns the parent tag which
            contains over 70% of the total text contained in the content tags
        *)

        let total = content |> List.fold (fun acc (x : NSoup.Nodes.Element) -> acc + (x.Text()).Length) 0
        let rec ProcessParents acc content = 
            match content with
            |[] -> acc
            | (hd : NSoup.Nodes.Element) :: tl -> 
                    match (GetParents hd) with
                    |Some(x) -> ProcessParents (x :: acc) tl
                    |None -> ProcessParents acc tl

        let parents = content |> MaybeMap (fun x -> GetParents x)

        let rec loop = function
            |_ when (parents |> List.length) = 0 -> None
            |[] -> ParentByStringContent parents
            | (hd : NSoup.Nodes.Element) :: tl -> 
                    match hd with
                    |_ when (float (hd.Text()).Length) / (float total) > 0.6 -> Some hd
                    |_ -> loop tl
        loop parents


    let KeepTry(document : NSoup.Nodes.Document) maxThreshold =
        
        let rec loop newThreshold =
            if newThreshold <= 0 then
                None
            else
                let maybe = new OptionBuilder()
                let attempt = maybe{
                    let! content = FindContent document newThreshold
                    let! parent = content |> ParentByStringContent
                    return parent
                }
                match attempt with
                |Some(x) -> attempt
                |None -> loop (newThreshold - 100)

        loop maxThreshold



    let GetAllLinks (baseLink : string) =
        let GetLinks (doc : NSoup.Nodes.Document) =
            let a = doc.Select("a.chp-release")
            a |> Seq.toList |> List.map (fun (x : NSoup.Nodes.Element) ->
                x.Attr("abs:href"))
        let CombineLinks (url1 : string) (url2 : string) =
            let url = 
                match (url1.EndsWith("/")) with
                |true -> url1
                |false -> (url1 + "/")
            let baseUrl = new Uri(url)
            (new Uri(baseUrl, url2))
        let rec loop (acc : List<string>) (next : string) =
            let newDoc = NSoupDownload ((CombineLinks baseLink next).ToString())
            match newDoc with
            |None -> None
            |Some(x) -> 
                    match (x.Select("a.next_page") |> Seq.toList) with
                    |[] -> Some (List.append acc (GetLinks(x)))
                    |_ -> loop (List.append acc (GetLinks(x))) (((x.Select("a.next_page") |> Seq.toList).Head).Attr("abs:href"))
        loop [] ""



