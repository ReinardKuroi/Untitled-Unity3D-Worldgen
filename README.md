# Untitled-Unity3D-Worldgen

For a long time I wanted to create a general use procedural terrain generator. Main goals are high performance, good looks, and the ability to customize density function from the editor. Long-term plan is to use this for a game, or possibly multiple games, to generate planets, asteroids, floating islands, flat terrain, e.c.t.

Most of my notes regarding this are in Obsidian. See [ReinardKuroi/worldgen](https://github.com/ReinardKuroi/worldgen) for some details.

### General plans

 - :white_check_mark: Terrain generation via noise + fade function (**CPU**)
 - :black_square_button: Terrain generation via noise + fade function (**GPU Compute**)
 - :white_check_mark: Chunks
 - :black_square_button: Dynamic chunks based on view distance
 - :white_check_mark: Collision mesh generation
 - :black_square_button: LOD based collision mesh for performance
 - :black_square_button: LOD meshes
 - :black_square_button: LOD meshes based on view distance
 - :black_square_button: Threaded generation
 - :black_square_button: Terrain modification via diff maps
 - :black_square_button: Saving terrain modification
 - :black_square_button: Terrain props (trees rocks items e.c.t)
 - :black_square_button: Saving terrain prop maps (allows for persistent saves)
 - :white_check_mark: Adaptive dual countouring with particle descent (**CPU**)
 - :black_square_button: Adaptive dual countouring with particle descent (**GPU Compute**)
 - :black_square_button: Marching cubes (**CPU**)
 - :black_square_button: Marching cubes (**GPU Compute**)
 - :black_square_button: Procedural atmospheric shader
 - :black_square_button: Procedural terrain shader
 - :black_square_button: Easily customizable density function
 - :black_square_button: Support for multiple terrain root points (planets orbiting each other e.c.t.)
 - :black_square_button: Extensive documentation
 - :black_square_button: while (true) { Refactoring(); }
 - ...
 
 ### Builds
 
 [**Test builds on GDrive**](https://drive.google.com/drive/u/0/folders/1DDYi-43jeXR510_j6G-nWEJRwGUw1O5E)
