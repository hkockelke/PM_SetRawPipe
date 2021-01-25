' ---------------------------------------------------------
'  Copyright Putzmeister Holding 2020
'  for internal usage only
'  November 27, 2020
'  set raw part of tube (create as component also handled)
'  Example Item 193590
' --------------------------------------------------------
Option Strict Off
Imports System
Imports System.Globalization
Imports System.Windows
Imports NXOpen
Imports NXOpenUI
Imports NXOpen.Features
Imports NXOpen.Assemblies
Imports NXOpen.UF
Imports NXOpen.BlockStyler

Module PM_Module_RawTubes

    Sub Main(ByVal args() As String)

        Dim b_debug As Boolean = False

        Dim theSession As Session = Session.GetSession()

        Dim theUfSession As UFSession = UFSession.GetUFSession()
        Dim theUISession As UI = UI.GetUI
        Dim lw As ListingWindow = theSession.ListingWindow

        '' needs to be set for non geometric children
        theUfSession.UF.SetVariable("UGII_ALLOW_NGC_IN_UGOPEN", "YES")

        lw.Open()

        Dim workPart As Part = theSession.Parts.Work
        Dim dispPart As Part = theSession.Parts.Display

        Dim theFeature As Feature = Nothing
        Dim stockFeature As Feature = Nothing
        Dim featureDiscp As String
        Dim featureAppl As String
        Dim attributeInfo As NXObject.AttributeInformation
        Dim nbStockFeature As Integer = 0
        Dim s_innerDiam As String = String.Empty
        Dim s_outerDiam As String = String.Empty
        Dim s_PipeShape As String = "Straight | Bent"
        Dim s_length As String
        Dim d_lenght As Double
        Dim PartIdRawTube As String = String.Empty
        Dim PartRevRawTube As String = String.Empty
        Dim b_doUpdate As Boolean = True

        If ((args.Length > 0) AndAlso (Not (String.IsNullOrEmpty(args(0))))) Then
            Dim s_debug = args(0)
            If String.Compare(s_debug, "debug", True) = 0 Then
                b_debug = True
            End If
        End If

        If (theSession.Parts.BaseWork Is Nothing) Then
            lw.WriteLine("Error: Active part required")
            Return
        End If

        Dim IsTcEng As Boolean = False
        theUfSession.UF.IsUgmanagerActive(IsTcEng)
        If (Not IsTcEng) Then
            lw.WriteLine("Error: Teamcenter is not running")
            Return
        End If

        '' ---------------------------------------------------
        '' Check: is there exact one Stock in this Assembly
        '' ---------------------------------------------------
        For Each theFeature In workPart.Features

            ' Routing Mechanical
            If theFeature.HasUserAttribute("APPLICATION", NXObject.AttributeType.String, -1) Then
                attributeInfo = theFeature.GetUserAttribute("APPLICATION", NXObject.AttributeType.String, -1)
                featureAppl = attributeInfo.StringValue
                lw.WriteLine("Feature APPLICATION: " & featureAppl)

                If (String.Compare(featureAppl, "Routing Mechanical", True) = 0) Then

                    lw.WriteLine("Feature name: " & theFeature.GetFeatureName())

                    ' Piping (if this attribute is not present, we have the case Stock as component, => no update of WorkPart attributes)
                    If theFeature.HasUserAttribute("DISCIPLINE", NXObject.AttributeType.String, -1) Then
                        attributeInfo = theFeature.GetUserAttribute("DISCIPLINE", NXObject.AttributeType.String, -1)
                        featureDiscp = attributeInfo.StringValue
                        lw.WriteLine("Feature DISCIPLINE: " & featureDiscp)
                        If (String.Compare(featureDiscp, "Piping", True) = 0) Then
                            b_doUpdate = True
                        Else
                            b_doUpdate = False
                        End If

                    Else
                        b_doUpdate = False
                    End If

                    stockFeature = theFeature
                    nbStockFeature = nbStockFeature + 1

                    If theFeature.HasUserAttribute("ID", NXObject.AttributeType.String, -1) Then
                        attributeInfo = theFeature.GetUserAttribute("ID", NXObject.AttributeType.String, -1)
                        lw.WriteLine(" Feature attr (Inner Diam): " & attributeInfo.StringValue)
                        s_innerDiam = attributeInfo.StringValue
                    End If
                    If theFeature.HasUserAttribute("OD", NXObject.AttributeType.String, -1) Then
                        attributeInfo = theFeature.GetUserAttribute("OD", NXObject.AttributeType.String, -1)
                        lw.WriteLine(" Feature attr (Outer Diam): " & attributeInfo.StringValue)
                        s_outerDiam = attributeInfo.StringValue
                    End If
                    If theFeature.HasUserAttribute("LENGTH", NXObject.AttributeType.Real, -1) Then
                        attributeInfo = theFeature.GetUserAttribute("LENGTH", NXObject.AttributeType.Real, -1)
                        lw.WriteLine(" Feature attr (LENGTH): " & attributeInfo.StringValue)
                        s_length = attributeInfo.StringValue
                        d_lenght = attributeInfo.RealValue
                    End If
                    'If theFeature.HasUserAttribute("PART_NUMBER", NXObject.AttributeType.String, -1) Then
                    '   attributeInfo = theFeature.GetUserAttribute("PART_NUMBER", NXObject.AttributeType.String, -1)
                    '   lw.WriteLine("Feature attr (PART_NUMBER): " & attributeInfo.StringValue)
                    'End IF
                    If theFeature.HasUserAttribute("MEMBER_NAME", NXObject.AttributeType.String, -1) Then
                        attributeInfo = theFeature.GetUserAttribute("MEMBER_NAME", NXObject.AttributeType.String, -1)
                        lw.WriteLine(" Feature attr (MEMBER_NAME): " & attributeInfo.StringValue)
                        PartIdRawTube = attributeInfo.StringValue
                        PartRevRawTube = getRev(PartIdRawTube)
                        lw.WriteLine("  Latest rev: " & PartRevRawTube)
                    End If

                    '' Debug: list all UserAttributes
                    If (b_debug) Then
                        Dim featureAttributeInfo As NXObject.AttributeInformation
                        lw.WriteLine("Debug - list all UserAttributes")
                        For Each featureAttributeInfo In theFeature.GetUserAttributes()
                            lw.WriteLine(" " & featureAttributeInfo.Title & " = " & featureAttributeInfo.StringValue)
                        Next
                        lw.WriteLine("Debug - End list all UserAttributes")
                    End If

                End If
            End If
        Next

        If (stockFeature Is Nothing) Then
            lw.WriteLine("no Stock Feature found")
            Return
        End If
        If (nbStockFeature > 1) Then
            lw.WriteLine("more then 1 Stock Feature found")
            Return
        End If

        '' ----------------------------------------------------------
        ''  check: is there already a NON Geo Part in the children
        '' ----------------------------------------------------------
        Dim theComp As ComponentAssembly = workPart.ComponentAssembly
        Dim theRootComponent As Component = theComp.RootComponent
        Dim b_NonGeoPart As Boolean = False
        Dim theChild As Component
        If (theRootComponent Is Nothing) Then
            lw.WriteLine("Not a root component: " & dispPart.Name)
            ' do not Return here
        Else

            ' UGII_ALLOW_NGC_IN_UGOPEN=YES needs to be set
            For Each theChild In theRootComponent.GetChildren()
                'check for Attribute: UG GEOMETRY
                ' lw.WriteLine("Child: " & theChild.DisplayName)
                If theChild.HasInstanceUserAttribute("UG GEOMETRY", NXObject.AttributeType.String, -1) Then
                    attributeInfo = theChild.GetInstanceUserAttribute("UG GEOMETRY", NXObject.AttributeType.String, -1)
                    'lw.WriteLine("Child attr (UG GEOMETRY): " & attributeInfo.StringValue)
                    ' Child attr (UG GEOMETRY): NO
                    If (String.Compare(attributeInfo.StringValue, "NO", True) = 0) Then
                        lw.WriteLine("There is already a Part with GEO=NO: " & theChild.DisplayName)
                        lw.WriteLine("Delete this Part to continue")
                        theUISession.NXMessageBox.Show("Part with GEOMETRY=NO", NXMessageBox.DialogType.Error, theChild.DisplayName)
                        Return
                    End If
                End If

                ' Debug: list all InstanceUserAttributes
                If (b_debug) Then
                    lw.WriteLine("Debug - Child: " & theChild.DisplayName)
                    Dim childAttributeInfo As NXObject.AttributeInformation
                    lw.WriteLine("Debug - list all Child InstanceUserAttributes")
                    For Each childAttributeInfo In theChild.GetInstanceUserAttributes(True)
                        lw.WriteLine(" " & childAttributeInfo.Title & " = " & childAttributeInfo.StringValue)
                    Next
                    lw.WriteLine("Debug - End list all Child InstanceUserAttributes")
                End If


            Next
        End If

        '' ------------------------------------------------        
        ''  Add Component to Assembly: the raw pipe
        '' ------------------------------------------------
        Dim RawPipeComponent As NXOpen.Assemblies.Component
        RawPipeComponent = addComponent(PartIdRawTube, PartRevRawTube)
        If (RawPipeComponent Is Nothing) Then
            lw.WriteLine("Can not add Component: " & PartIdRawTube & "/" & PartRevRawTube)
            Return
        End If

        '' ---------------------------------------------------------  
        '' Change Properties:
        ''  Non geometry (UG Geometry = NO)
        '' add length in Size / Dimension 
        '' from Properties of Stock
        '' Format 1234.1 (4 digits and 1 decimal digit)
        '' Raw unit type BOM = MM (fix)
        '' ---------------------------------------------------------  
        lw.WriteLine("Set Instance User Attribute for: " & PartIdRawTube)

        RawPipeComponent.SetInstanceUserAttribute("UG GEOMETRY", 0, "NO", Update.Option.Now)
        lw.WriteLine(" UG GEOMETRY=" & "NO")

        Dim formatLength As String = formatReal(d_lenght)
        RawPipeComponent.SetInstanceUserAttribute("PM5_SIZE_DIM_BOM", 0, formatLength, Update.Option.Now)
        lw.WriteLine(" Size / Dimension (PM5_SIZE_DIM_BOM)=" & formatLength)

        RawPipeComponent.SetInstanceUserAttribute("PM5_UOM_BOM", 0, "MM", Update.Option.Now)
        lw.WriteLine(" Raw-Unit type BOM (PM5_UOM_BOM)=" & "MM")

        ' Debug: list all InstanceUserAttributes
        If (b_debug) Then
            Dim CompAttributeInfo As NXObject.AttributeInformation
            lw.WriteLine("Debug - list all InstanceUserAttributes")
            For Each CompAttributeInfo In RawPipeComponent.GetInstanceUserAttributes(True)
                lw.WriteLine(" " & CompAttributeInfo.Category & ":" & CompAttributeInfo.Title & " = " & CompAttributeInfo.StringValue)
            Next
            lw.WriteLine("Debug - End list all InstanceUserAttributes")
        End If

        '' --------------------------------------------------------- 
        ''  set Inner/ Outer Diameter and Length of the Part itself
        '' --------------------------------------------------------- 
        'Dim response As Integer
        'response = theUISession.NXMessageBox.Show(workPart.Name, NXMessageBox.DialogType.Question, "Set User Attribute?")
        'If response = 1 Then
        '    lw.WriteLine("Set User Attribute = Yes")
        '    b_doUpdate = True
        'Else
        '    lw.WriteLine("Set User Attribute = No")
        '    b_doUpdate = False
        'End If

        If (b_doUpdate) Then
            lw.WriteLine("Set User Attribute for: " & workPart.Name)

            workPart.SetUserAttribute("LENGTH", -1, formatLength, Update.Option.Now)
            lw.WriteLine(" LENGTH=" & formatLength)

            workPart.SetUserAttribute("INNERDIAMETER", -1, s_innerDiam, Update.Option.Now)
            lw.WriteLine(" INNERDIAMETER=" & s_innerDiam)

            workPart.SetUserAttribute("OUTERDIAMETER", -1, s_outerDiam, Update.Option.Now)
            lw.WriteLine(" OUTERDIAMETER=" & s_outerDiam)

        End If

        '' todo: UI for shape attribute
        ''s_PipeShape = showUI_toggle_PipeShape()
        ''lw.WriteLine(" Pipe-Shape=" & s_PipeShape)


        lw.Close()

    End Sub
    ''' <summary>
    ''' Format a Double value to one digit after the decimal separator
    ''' </summary>
    ''' <param name="value"></param>
    ''' <returns>string</returns>
    Private Function formatReal(ByVal value As Double) As String
        formatReal = "0.0"
        Dim nfi As NumberFormatInfo = New CultureInfo("en-US", False).NumberFormat
        formatReal = value.ToString("F1", nfi)
    End Function
    ''' <summary>
    ''' get latest revision as string
    ''' </summary>
    ''' <param name="PartId"></param>
    ''' <returns>string the Revision</returns>
    Private Function getRev(ByVal PartId As String) As String
        getRev = "-"
        Dim theUISession As UI = UI.GetUI
        Dim theUfSession As UFSession = UFSession.GetUFSession()
        Dim ugmgr As UFUgmgr = theUfSession.Ugmgr
        Dim revcount As Integer
        Dim revstr() As Tag = Nothing
        Dim parttag As Tag
        Dim revision As String = String.Empty

        'lw.WriteLine("Finding latest revision for part " & PartId)
        Try
            ugmgr.AskPartTag(PartId, parttag)
            ugmgr.ListPartRevisions(parttag, revcount, revstr)
            ugmgr.AskPartRevisionId(revstr(revcount - 1), revision)
            getRev = revision
        Catch ex As Exception
            ''MsgBox("Error occurred loading part number " & PartId)
            theUISession.NXMessageBox.Show("Error occurred", NXMessageBox.DialogType.Error, "Loading part number " & PartId)
            'lw.WriteLine("Error occurred loading part number " & PartId)
        End Try

    End Function
    ''' <summary>
    ''' UI to get the Pipe/Tube-Shape
    ''' </summary>
    ''' <returns>String: Bent or Straight</returns>
    Private Function showUI_toggle_PipeShape() As String
        showUI_toggle_PipeShape = "Bent"
        Try
            Dim theSession As Session = Session.GetSession()
            Dim theUISession As UI = UI.GetUI

            ''Dim theDialogName As String = "Toggle_PipeShape.dlx"
            ''Dim theDialog As BlockDialog = theUISession.CreateDialog(theDialogName)
            Dim retunValue As Integer = theUISession.NXMessageBox.Show("Pipe Shape", NXMessageBox.DialogType.Question, "Bent")
            If (retunValue = 1) Then
                showUI_toggle_PipeShape = "Bent"
            ElseIf (retunValue = 2) Then
                showUI_toggle_PipeShape = "Straight"
            End If

            Dim lw As ListingWindow = theSession.ListingWindow
            lw.WriteLine("Response: " & retunValue.ToString())

        Catch ex As Exception
            showUI_toggle_PipeShape = "NA"
        End Try
    End Function
    ''' <summary>
    ''' add component: taken from journal (to be improved)
    ''' </summary>
    ''' <param name="PartId"></param>
    ''' <param name="PartRev"></param>
    ''' <returns></returns>
    Private Function addComponent(ByVal PartId As String, ByVal PartRev As String) As NXOpen.Assemblies.Component
        addComponent = Nothing
        Dim theSession As Session = Session.GetSession()
        Dim lw As ListingWindow = theSession.ListingWindow
        Dim workPart As Part = theSession.Parts.Work

        lw.WriteLine("Add Part: " & PartId & " Revision: " & PartRev)

        If (String.IsNullOrEmpty(PartId)) Then
            lw.WriteLine("Error Add Part: " & PartId & " Revision: " & PartRev)
            Return Nothing
        End If

        ' ----------------------------------------------
        '   Menu: Assemblies->Components->Add Component...
        ' ----------------------------------------------
        Dim markId1 As NXOpen.Session.UndoMarkId = Nothing
        markId1 = theSession.SetUndoMark(NXOpen.Session.MarkVisibility.Visible, "Start")

        Dim addComponentBuilder1 As NXOpen.Assemblies.AddComponentBuilder = Nothing
        addComponentBuilder1 = workPart.AssemblyManager.CreateAddComponentBuilder()

        Dim componentPositioner1 As NXOpen.Positioning.ComponentPositioner = Nothing
        componentPositioner1 = workPart.ComponentAssembly.Positioner

        componentPositioner1.ClearNetwork()

        ''Manages the suppression of NXOpen.Assemblies.Component s within a NXOpen.Assemblies.ComponentAssembly .
        ''Currently, an Arrangement simply acts As a context within which a Component can be suppressed, unsuppressed, Or positioned.
        '' code below not needed here ...

        ' Dim arrangement1 As NXOpen.Assemblies.Arrangement = CType(workPart.ComponentAssembly.Arrangements.FindObject("Arrangement 1"), NXOpen.Assemblies.Arrangement)
        ' componentPositioner1.PrimaryArrangement = arrangement1

        componentPositioner1.BeginAssemblyConstraints()

        Dim allowInterpartPositioning1 As Boolean = Nothing
        allowInterpartPositioning1 = theSession.Preferences.Assemblies.InterpartPositioning

        Dim nullNXOpen_Unit As NXOpen.Unit = Nothing

        Dim expression1 As NXOpen.Expression = Nothing
        expression1 = workPart.Expressions.CreateSystemExpressionWithUnits("1.0", nullNXOpen_Unit)

        Dim unit1 As NXOpen.Unit = CType(workPart.UnitCollection.FindObject("MilliMeter"), NXOpen.Unit)

        Dim expression2 As NXOpen.Expression = Nothing
        expression2 = workPart.Expressions.CreateSystemExpressionWithUnits("6.28319", unit1)

        Dim expression3 As NXOpen.Expression = Nothing
        expression3 = workPart.Expressions.CreateSystemExpressionWithUnits("0.0", unit1)

        Dim unit2 As NXOpen.Unit = CType(workPart.UnitCollection.FindObject("Degrees"), NXOpen.Unit)

        Dim expression4 As NXOpen.Expression = Nothing
        expression4 = workPart.Expressions.CreateSystemExpressionWithUnits("0.0", unit2)

        Dim expression5 As NXOpen.Expression = Nothing
        expression5 = workPart.Expressions.CreateSystemExpressionWithUnits("1", nullNXOpen_Unit)

        Dim expression6 As NXOpen.Expression = Nothing
        expression6 = workPart.Expressions.CreateSystemExpressionWithUnits("6.28319", unit1)

        Dim expression7 As NXOpen.Expression = Nothing
        expression7 = workPart.Expressions.CreateSystemExpressionWithUnits("0", unit1)

        Dim expression8 As NXOpen.Expression = Nothing
        expression8 = workPart.Expressions.CreateSystemExpressionWithUnits("0", unit2)

        Dim network1 As NXOpen.Positioning.Network = Nothing
        network1 = componentPositioner1.EstablishNetwork()

        Dim componentNetwork1 As NXOpen.Positioning.ComponentNetwork = CType(network1, NXOpen.Positioning.ComponentNetwork)

        componentNetwork1.MoveObjectsState = True

        Dim nullNXOpen_Assemblies_Component As NXOpen.Assemblies.Component = Nothing

        componentNetwork1.DisplayComponent = nullNXOpen_Assemblies_Component

        theSession.SetUndoMarkName(markId1, "Add Component Dialog")

        componentNetwork1.MoveObjectsState = True

        Dim markId2 As NXOpen.Session.UndoMarkId = Nothing
        markId2 = theSession.SetUndoMark(NXOpen.Session.MarkVisibility.Invisible, "Assembly Constraints Update")

        Dim nullNXOpen_Assemblies_ProductInterface_InterfaceObject As NXOpen.Assemblies.ProductInterface.InterfaceObject = Nothing

        addComponentBuilder1.SetComponentAnchor(nullNXOpen_Assemblies_ProductInterface_InterfaceObject)

        addComponentBuilder1.SetInitialLocationType(NXOpen.Assemblies.AddComponentBuilder.LocationType.WorkPartAbsolute)

        addComponentBuilder1.SetCount(1)

        addComponentBuilder1.SetScatterOption(True)

        Dim markId3 As NXOpen.Session.UndoMarkId = Nothing
        markId3 = theSession.SetUndoMark(NXOpen.Session.MarkVisibility.Invisible, "Start")

        theSession.SetUndoMarkName(markId3, "Part file name Dialog")

        ' ----------------------------------------------
        '   Dialog Begin Part file name
        ' ----------------------------------------------
        Dim markId5 As NXOpen.Session.UndoMarkId = Nothing
        markId5 = theSession.SetUndoMark(NXOpen.Session.MarkVisibility.Invisible, "Part file name")

        theSession.DeleteUndoMark(markId5, Nothing)

        theSession.SetUndoMarkName(markId3, "Part file name")

        theSession.DeleteUndoMark(markId3, Nothing)

        addComponentBuilder1.ReferenceSet = "Use Model"

        addComponentBuilder1.Layer = -1

        Dim partstouse1(0) As NXOpen.BasePart
        Dim s_PartString As String = PartId & "/" & PartRev
        Dim partString As String = "@DB/" & s_PartString

        theSession.Parts.SetNonmasterSeedPartData(partString)

        Dim basePart1 As NXOpen.BasePart = Nothing
        Dim partLoadStatus1 As NXOpen.PartLoadStatus = Nothing
        Dim part1 As NXOpen.Part = Nothing

        '' did not found a better solution here
        Dim b_alreadOpen As Boolean = False
        For Each openPart As Part In theSession.Parts
            'lw.WriteLine("part: " & openPart.FullPath)
            If (String.Compare(openPart.FullPath, s_PartString) = 0) Then
                'part is already open
                b_alreadOpen = True
                Exit For
            End If
        Next

        If b_alreadOpen Then
            part1 = CType(theSession.Parts.FindObject(partString), NXOpen.Part)
        Else
            basePart1 = theSession.Parts.OpenBase(partString, partLoadStatus1)
            part1 = CType(basePart1, NXOpen.Part)
        End If

        partstouse1(0) = part1
        addComponentBuilder1.SetPartsToAdd(partstouse1)

        '' Warning used var productinterfaceobjects1
        '' Returns all product interface objects available, one of these can be used as component anchor
        Dim productinterfaceobjects1() As NXOpen.Assemblies.ProductInterface.InterfaceObject
        addComponentBuilder1.GetAllProductInterfaceObjects(productinterfaceobjects1)

        Dim movableObjects1(0) As NXOpen.NXObject

        '' "COMPONENT 161567/A 1"
        Dim componentString As String = "COMPONENT " & s_PartString & " 1"
        Dim component1 As NXOpen.Assemblies.Component = CType(workPart.ComponentAssembly.RootComponent.FindObject(componentString), NXOpen.Assemblies.Component)

        movableObjects1(0) = component1
        componentNetwork1.SetMovingGroup(movableObjects1)

        Dim markId7 As NXOpen.Session.UndoMarkId = Nothing
        markId7 = theSession.SetUndoMark(NXOpen.Session.MarkVisibility.Invisible, "Add Component")

        Dim markId8 As NXOpen.Session.UndoMarkId = Nothing
        markId8 = theSession.SetUndoMark(NXOpen.Session.MarkVisibility.Invisible, "AddComponent on_apply")

        componentNetwork1.Solve()

        componentPositioner1.ClearNetwork()

        Dim nErrs1 As Integer = Nothing
        nErrs1 = theSession.UpdateManager.AddToDeleteList(componentNetwork1)

        Dim nErrs2 As Integer = Nothing
        nErrs2 = theSession.UpdateManager.DoUpdate(markId2)

        componentPositioner1.EndAssemblyConstraints()

        '' Warning used out var logicalobjects1
        '' Returns the PDM.LogicalObjects having unassigned non-auto-assignable required attributes.
        Dim logicalobjects1() As NXOpen.PDM.LogicalObject
        addComponentBuilder1.GetLogicalObjectsHavingUnassignedRequiredAttributes(logicalobjects1)

        addComponentBuilder1.ComponentName = PartId

        Dim nXObject1 As NXOpen.NXObject = Nothing
        nXObject1 = addComponentBuilder1.Commit()

        Dim errorList1 As NXOpen.ErrorList = Nothing
        errorList1 = addComponentBuilder1.GetOperationFailures()

        errorList1.Dispose()
        theSession.DeleteUndoMark(markId7, Nothing)

        theSession.SetUndoMarkName(markId1, "Add Component")

        addComponentBuilder1.Destroy()

        Dim nullNXOpen_Assemblies_Arrangement As NXOpen.Assemblies.Arrangement = Nothing

        componentPositioner1.PrimaryArrangement = nullNXOpen_Assemblies_Arrangement

        theSession.DeleteUndoMark(markId2, Nothing)

        '' return the added component
        addComponent = component1

    End Function


    Public Function GetUnloadOption(ByVal dummy As String) As Integer

        'Unloads the image immediately after execution within NX
        GetUnloadOption = NXOpen.Session.LibraryUnloadOption.Immediately

    End Function

End Module
