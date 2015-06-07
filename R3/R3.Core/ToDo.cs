namespace R3.Core
{
#if false
		
	MagicTile Version2 List

	// ZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZ

	v2 overall top priorities
	+ all C# code
	+ general {p,q}
	+ better slicing for above
	+ rendering improvements (render-to-texture)
	+ hyperbolic/spherical panning
	+ new puzzle config and menu building
	+ new puzzles:  7-color torus, klein bottle, "petals", "edge-only", "vertex circles", Big Chop

	Polish
	- Make mouse move not speed up "wait circles" when building puzzle.
	- Solve beep can happen too much, if user continues twisting after a solve.
	- escape should stop recording of setup moves
	- Add pictures and metadata info (the latter suggested by Melinda) to puzzle tree
	- When loading puzzle file you have open already, do nothing. (?)

	Macros
	+ Click handling needs to record which keys are pressed.
	+ TwistHistory needs to hold single twists or macros.  Abstract the choice between these as a "move"?  No, I think we just want a list of twists.
	+ Persistence (think of a clean format).  Main question is whether to have m[ be individual items in list (like Andrey) or part of twist, e.g. m[:8:R:0.
		I think I like the latter better (total number of items is still number of twists), plus twists can completely load themselves.  Just use '[', not "m["
	+ Macro reorientation handling (do like Andrey).
	+ Should they be stored with puzzle?  Let's do it and see what happens.
	+ Should we enforce running only on correct puzzle?  Yes, provide a useful message about which puzzle macros were created for.
		Can't run same macros on ET and VT, even if same tiling.
		Possible to run same macros on two different versions of {5,5}?  Maybe just not allow this anyway?
	+ All macros from multiple puzzles in one macro file?  Then I have to worry about graying out non-applicable macros and what not.  Start out not doing this.
	+ Test loading/saving old files.
	- UI to select macros, add/remove/reorder...  REORDER still to do.
	+ UI to alert we are recording? (blinking light)
	+ applying macros when building macros?  Make this work, and test it.
	+ Test in different geometries.
	- Nan requested setup move features working recursively  (see email 11-20-11)

	Skew
	+ Check most current matrix code in KQViewer.  Differences?
	+ Correctly choose 3D/4D mouse controls
	+ Update mouse help.
	+ Fix setting organization.
	+ 5-cell variants (http://eusebeia.dyndns.org/4d/runci5cell)
	- Improve identifications on runcinated 5-cell.  Haven't been able to get this without drawing 20k tiles, so I'm going to leave as is :(
	+ Better starting positions (include 3D camera).
	+ Choose good default projection settings. (leanding towards stereographic)
	+ Make projections to large distances look better somehow.  Maybe there is some OpenGL clipping I could leverage?  ...Settled on making ProjectTo3D() "safe".
	- Fix incorrect drawing for very large spheres.  Maybe have a check of triangle size, and don't draw triangles above a certain threshold.
	- allow 1-sided textures, to hide things?  Or a shrink setting for faces?
	- Smooth out lighting normals in stereographic mode?  I kind of like the faceted look, and it's easier to program.
	- extra level of texture detail would be good.
	- Coding work to rename Vector3D to Vector4D

	Slices
	+ Test loading older files (Make sure saved 0 slicemasks work as slicemask 1).
	+ 2x2x2 doesn't work right (line slices confusig things).
	+ Rendering of appropriate circles based on mask.
	+ What to do about puzzles like {3,5} 8C?  It behaves really strangely for slice-2 twists, so we need to disallow them.
	+ Hemi-puzzles look ok.

	IRP
	+ Put cells in correct order.
	+ Repeating fundamental unit.
	+ Make IRP show by default in settings.
	+ Twisting (selecting nearest twisting circle)
	- Highlighting twisting circles.
	- Automate finding identification configuration for hyperbolic plane?
	- CircumCircle function not working when making IRP cells because it is not 3D.  Don't think we need to worry about this now.
	- Clean up code (move to separate files).
	
	Ideas
	- 1-20-12: New kind of twisting in Euclidean/Hyperbolic with no spherical analogue.  
		Slicing circles of infinite radius.  Translations and Horocycle rotations!  This might be hard to implement, since so much of puzzle will get affected.
		I think this will only work in Euclidean

	Operation Puzzle Explosion
	+ Macros broken on spherical inverted faces, e.g. Rubik's Cube
	+ Add error about loading preview ET/VT puzzles.
	+ Menu options to jump to puzzle tree, settings pane, or macros pane.
	+ Fix macro check for what puzzles things can run on (needs to check properties instead of ID). (avoided this by saving old puzzle IDs)
	+ Append names rather than replace? would be good for new Megaminxs.  I'm leaning to yes on this.
	+ Counter to show how many puzzles there are.
	- Fix Slicers() function in puzzle to grab enough tiles.  Use {6,3} F1:1:1 as a test case for this.
	- {6,3} V0:1:0 beats up slicing function.
	- Profile puzzle building when lots of twisting, e.g. {8,4} EVT.  We need to speed up the method MarkCellsForStateCalcs.
	- Remember the expansion/contraction of settings pane, and probably window position too.
	- auto name "Petals"?
	- module test!
	  runs through all puzzles, build, scramble, save, and compare output.
	  could maybe auto-gen images of puzzles when doing this.
	- when puzzle build fails, all hell breaks loose and program will end up crashing.  Not as bad when puzzle load fails.

	Melinda 6-11-11
	+ Stereo

	Melinda 6-5
	+ Change Twisting of inverted cells.

	Melinda 5-14
	* Let the panning continue even after dragging outside the puzzle boundary? I know that in some ways that doesn't make sense, but as a user, it appears like it should be possible.
	* Add option for interaction sounds like in MC4D.

	Code cleanup
	- Polygon.CreateEuclidean is a duplicate of FromPoints.  Merge these methods.
	- I don't like overall organization, e.g. that renderer contains twist handler (done to give access to render-to-texture).  
	  Would be nice to have an uber-puzzle object to pass around.
	- Tiling.ReflectRecursive, remove "completed" Dictionary (can use member variable)
	- cleanup Mobius Unity/Identity
	- Shouldn't be creating new imagespace everytime UpdateImageSpace is called.
	- Make some Vector3D methods properties, e.g. MagSquared, Abs
	- make Vector3D a struct instead of a class?
	- NearTree.FindCloseObjectsRecursive (clean up like I did for FindNearest)
	- Look into Clone thing (I don't like how it is now).
	- ugly manual copying of tiles in tiling.cs
	
	Bugs
	- Building not totally safe if user is clicking around in GL window or settings while it is happening.  I have seen clicking stop working.
	- changing rotation rate with slider doesn't affect things if currently twisting.
	- some twisting circles drawn when cells are not
	- cocentric slicing circles broken
	+ octahedron drawing bug.
	+ Hyperbolic/spherical distance metric on NearTree
	- Better infinity checking? e.g. see Segment.Tranform
	- Twisting circle drawing doesn't pay attention to showOnlyFundamental setting.
	- ShowOnlyFundamental=true <- doesn't work right for spherical puzzles during a twist.

	Features
	- Allow panning to continue and dampen out!
	- Puzzle building feedback (make base Tiling generation also report progress).  Catch exceptions when doing this.
	- Add puzzle description.
	- save things like whether settings are shown, last puzzle, etc.
	+ support for orientable 9-color {4,4} will require new features (allow reflecting at end, like current rotate at end)
	- auto-downloading of new puzzles from net.
	- should remember if showed settings last time program ran.
	- Make highlighting not instantaneous (like universe sandbox)
	- scramble should also arbitrarily reorient puzzle, to avoid "cheating" Andrey described on 3-23-11 post.

	Performance
	- Menu building at the beginning feels a little slow.
	- Charlie's idea (see email 5/5/2011).
	- performance slider that controls culling of small cells

//////////////////////////////////////////// Everything below this line is Finished

	Menu thoughts:
	menu created all from puzzle config which lives on disk (users can customize menus this way).
	what to do about puzzles which should live in multiple places in menu?
	tags for puzzles, so they could show in different places?  genus-3, hyperbolic, by vertex-figure, difficulty, etc.

	circle definition, example on 2,3,6 triangle.
	right definition a linear combination of 3 sides of fundamental triangle?  plus a fourth constant to handle additional cases?
	a*inradius + b*circumradius + c* + d

	Code cleanup
	- Make Mobius a class?
	- tile.Boundary.Center -> tile.Center?
	- Polygon.GetEdgePoints should return array, not list

	Features
	- Identification work (use yield keyword to help).
	- vertex and edge centered twists?  This really shouldn't be much more difficult at all.  Maybe it would allow easier coding of face-turning uniform tilings too.
	- better persistence for identification.  It's too ugly.
	- slicing circles highlight in realtime to show what twisting is possible.  draw on texture though.
	- slicing circle highlight color.
	- normalize twisting speed based on twist order.
	- To support klein bottle, we need a stride in identification config.
	* Ctrl+S for saving (Andrey 3-2-11)
	* A way to reset settings to defaults (Melinda 3-6-11)
	* Put the full name of the puzzle & length in the title (Melinda 3-6-11)
	* Make sure that no scrambling twists undo the previous twist. It happens enough to be noticeable (Melinda 3-6-11)
	- User puzzles? not yet.

	Bugs
	- FIXED: mouse dragging goes wack after a while
	- FIXED: {3,7} identification problem.
	- weird missing white tile on {7,3} (increasing FP tolerance fixed this)
	- double-click color crashes.
	- Slicing circle thickness is not correct (in the respective geometries)
	- when settings first show, they are scrolled to the bottom
	- center sticker disappearing sometime on {3,7} puzzle during twist.  green face on {7,3} doing this too.
	    I think this is same problem in CalculateFromTwoPolygonsInternal (see code there).
	- can't really drag while twisting (seems to halt timer).
	- load puzzle while solving...crash.
	- puzzle loading broken due to threading
	- when puzzle loads, circles are not highlighted.

	Performance
	- memory: don't store stickers for all cells.
	- better texture LOD (like a quadtree, but with triangles).
	- don't regen textures each time (invalidate those that change).

//////////////////////////////////////////// Other

	// ZZZ - Old MagicTile probs
	// DrawEdge points has nested loops with same variable.
	// Also VectorND has reset and empty methods which do the same thing.
	// Segment::Reflect was broken (as evidenced by {3,5} tiling.  I did it much simpler here, and perhaps should copy to there.
	// Update the magic formula to handle non-simplex vertex figures.
	// Change Polygon::orientation method and use in Polygon::slice to match what I did here?
	// PointOnArcSegment changed to be tolerance safe.

	// Silverlight 4 tools for VS
	// http://www.microsoft.com/downloads/details.aspx?FamilyID=40ef0f31-cb95-426d-9ce0-00dcfabf3df5&displaylang=en

	// Silverlight drawing efficiency: 
	// http://stackoverflow.com/questions/2252084/most-efficient-way-to-draw-in-silverlight
	// http://msdn.microsoft.com/en-us/magazine/dd483292.aspx

#endif
}
