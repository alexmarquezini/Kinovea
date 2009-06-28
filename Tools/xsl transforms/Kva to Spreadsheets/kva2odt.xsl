﻿<?xml version="1.0" encoding="UTF-8"?>
<xsl:stylesheet 
  version="1.0" 
  xmlns:xsl="http://www.w3.org/1999/XSL/Transform"  
  xmlns:office="urn:oasis:names:tc:opendocument:xmlns:office:1.0"
  xmlns:style="urn:oasis:names:tc:opendocument:xmlns:style:1.0"
  xmlns:fo="urn:oasis:names:tc:opendocument:xmlns:xsl-fo-compatible:1.0"
  xmlns:number="urn:oasis:names:tc:opendocument:xmlns:datastyle:1.0" 
  xmlns:text="urn:oasis:names:tc:opendocument:xmlns:text:1.0"
  xmlns:table="urn:oasis:names:tc:opendocument:xmlns:table:1.0">

<!-- 
	www.kinovea.org
	
	This stylesheet formats a .kva file to OpenOffice.org spreadsheet.
	You can use it to view the content of the .kva  in OpenOffice.org.
	
	Usage:
	  - to be included in a Filter Extension.
-->

<xsl:template match="/">
	<office:document-content office:version="1.0">
		
		<office:automatic-styles>
      
      <!-- Style for Keyframes header -->
      <style:style style:name="ce1" style:family="table-cell" style:parent-style-name="Default">
        <style:table-cell-properties fo:background-color="#d2f5b0"/>
        <style:text-properties fo:font-weight="bold" style:font-weight-asian="bold" style:font-weight-complex="bold"/>
      </style:style>
      
      <!-- Style for Tracks header -->
      <style:style style:name="ce2" style:family="table-cell" style:parent-style-name="Default">
        <style:table-cell-properties fo:background-color="#c2dfff"/>
        <style:text-properties fo:font-weight="bold" style:font-weight-asian="bold" style:font-weight-complex="bold"/>      
      </style:style>

      <!-- Style for Chronos header -->
      <style:style style:name="ce3" style:family="table-cell" style:parent-style-name="Default">
        <style:table-cell-properties fo:background-color="#e3b7eb"/>
        <style:text-properties fo:font-weight="bold" style:font-weight-asian="bold" style:font-weight-complex="bold"/>
      </style:style>
      
      <!-- Style for data values -->
      <style:style style:name="ce4" style:family="table-cell" style:parent-style-name="Default">
        <style:table-cell-properties fo:background-color="#e8e8e8"/>
      </style:style>

      <!-- Style for x,y headers values -->
      <style:style style:name="ce5" style:family="table-cell" style:parent-style-name="Default">
        <style:table-cell-properties fo:background-color="#e8e8e8" style:text-align-source="fix" style:repeat-content="false"/>
        <style:paragraph-properties fo:text-align="center" fo:margin-left="0cm"/>      
      </style:style>

		</office:automatic-styles>

		<office:body>
			<office:spreadsheet>
				<table:table>
					<xsl:attribute name="table:name"> 
						<xsl:value-of select="KinoveaVideoAnalysis/OriginalFilename" />
					</xsl:attribute>
					
          <table:table-column/>
					<table:table-column/>					
					
					<xsl:apply-templates select="//Keyframes"/>
					<xsl:apply-templates select="//Tracks"/>
					<xsl:apply-templates select="//Chronos"/>
				
				</table:table>
			</office:spreadsheet>
		</office:body>
	</office:document-content>
</xsl:template>


<xsl:template match="Keyframes">

	<table:table-row>
    <table:table-cell table:style-name="ce1"><text:p>Images clés</text:p></table:table-cell>
  </table:table-row>	

	<xsl:for-each select="Keyframe">
		<table:table-row>
			<table:table-cell table:style-name="ce4"><text:p>Titre :</text:p></table:table-cell>
			<table:table-cell table:style-name="ce4"><text:p><xsl:value-of select="Title" /></text:p></table:table-cell>
		</table:table-row>
	</xsl:for-each>
	
</xsl:template>

<xsl:template match="Tracks">

	<table:table-row><table:table-cell><text:p></text:p></table:table-cell></table:table-row>
	<xsl:for-each select="Track">
		<table:table-row><table:table-cell><text:p></text:p></table:table-cell></table:table-row>
		<!-- Track table -->
		<table:table-row>
			<table:table-cell table:style-name="ce2"><text:p>Trajectoire</text:p></table:table-cell>
		</table:table-row>
		<table:table-row>
			<table:table-cell table:style-name="ce4"><text:p>Label :</text:p></table:table-cell>
			<table:table-cell table:style-name="ce4"><text:p><xsl:value-of select="Label/Text" /></text:p></table:table-cell>
		</table:table-row>
		<table:table-row>
			<table:table-cell table:style-name="ce4"><text:p>Coordonnées (pixels)</text:p></table:table-cell>
      <table:table-cell table:style-name="ce4"><text:p></text:p></table:table-cell>		
		</table:table-row>
		<table:table-row>
			<table:table-cell table:style-name="ce5"><text:p>x</text:p></table:table-cell>
			<table:table-cell table:style-name="ce5"><text:p>y</text:p></table:table-cell>
		</table:table-row>
		<xsl:for-each select="TrackPositionList/TrackPosition">
			<table:table-row>
				<xsl:call-template name="tokenize">
					<xsl:with-param name="inputString" select="."/>
                  	<xsl:with-param name="separator" select="';'"/>
          		</xsl:call-template>
			</table:table-row>
		</xsl:for-each>
	</xsl:for-each>	

</xsl:template>

<xsl:template match="Chronos">

	<table:table-row><table:table-cell><text:p></text:p></table:table-cell></table:table-row>
	<xsl:for-each select="Chrono">
		<table:table-row><table:table-cell><text:p></text:p></table:table-cell></table:table-row>
		<table:table-row>
			<table:table-cell table:style-name="ce3"><text:p>Chrono</text:p></table:table-cell>
		</table:table-row>
		<table:table-row>
			<table:table-cell table:style-name="ce4"><text:p>Label :</text:p></table:table-cell>
			<table:table-cell table:style-name="ce4"><text:p><xsl:value-of select="Label/Text" /></text:p></table:table-cell>
		</table:table-row>
		<table:table-row>
			<table:table-cell table:style-name="ce4"><text:p>Durée (ts)</text:p></table:table-cell>
			<table:table-cell>
			  <xsl:attribute name="table:style-name">
          <xsl:value-of select="'ce4'"/>      
        </xsl:attribute>
			  <xsl:attribute name="office:value-type">
          <xsl:value-of select="'float'"/>      
        </xsl:attribute>
			  <xsl:attribute name="office:value">
          <xsl:value-of select="Values/StopCounting - Values/StartCounting"/>      
        </xsl:attribute>
			  <text:p><xsl:value-of select="Values/StopCounting - Values/StartCounting" /></text:p></table:table-cell>
		</table:table-row>
	</xsl:for-each>

</xsl:template>

<xsl:template name="tokenize">
	<xsl:param name="inputString"/>
	<xsl:param name="separator" select="';'"/>
	
	<!-- 
	This function will not output the last element because there is no separator after it.
	So the last call to this (recursive) function is a single number and "substring-before" fails. 
	-->
	
	<!-- Split next value from the rest -->
	<xsl:variable name="token" select="substring-before($inputString, $separator)" />
	<xsl:variable name="nextToken" select="substring-after($inputString, $separator)" />
	
	<!-- output numeric value. ref : http://books.evc-cit.info/odbook/ch05.html#table-number-cells-section -->
	<xsl:if test="$token">
    <table:table-cell>
      <xsl:attribute name="table:style-name">
        <xsl:value-of select="'ce4'"/>      
      </xsl:attribute>
      <xsl:attribute name="office:value-type">
        <xsl:value-of select="'float'"/>      
      </xsl:attribute>
      <xsl:attribute name="office:value">
        <xsl:value-of select="$token"/>      
      </xsl:attribute>
        <text:p><xsl:value-of select="$token"/></text:p>
    </table:table-cell>
	</xsl:if>
 
	<!-- recursive call to tokenize for the rest -->
	<xsl:if test="$nextToken">
		<xsl:call-template name="tokenize">
			<xsl:with-param name="inputString" select="$nextToken"/>
            <xsl:with-param name="separator" select="$separator"/>
        </xsl:call-template>
	</xsl:if>
</xsl:template>

</xsl:stylesheet>