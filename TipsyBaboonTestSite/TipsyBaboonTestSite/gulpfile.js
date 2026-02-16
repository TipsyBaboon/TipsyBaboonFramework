'use strict'

var gulp = require('gulp');
var sassCompiler = require('sass');
var sass = require('gulp-sass')(sassCompiler);
var sourcemaps = require('gulp-sourcemaps');
var rename = require('gulp-rename');

// =============================================================================
// SASS BUILD CONFIGURATION
// =============================================================================
// Build SCSS from ClientApp/sass/layout/site.scss to wwwroot/css/style.css
// =============================================================================

// -----------------------------------------------------------------------------
// SASS task - compiles SCSS to wwwroot/css/style.css
// -----------------------------------------------------------------------------
gulp.task('sass', function () {
    return gulp.src('./ClientApp/sass/layout-tb-light/site.scss')
        .pipe(sourcemaps.init())
        .pipe(sass({
            outputStyle: 'expanded',
            includePaths: ['node_modules'],
            // Silence Bootstrap-internal deprecations that we can't fix without
            // breaking changes. These are expected until Bootstrap 6 migrates to @use.
            // Review when upgrading Bootstrap or Dart Sass major versions.
            silenceDeprecations: ['legacy-js-api', 'import', 'global-builtin', 'if-function']
        }).on('error', sass.logError))
        .pipe(rename('style.css'))
        .pipe(sourcemaps.write('./maps'))
        .pipe(gulp.dest('./wwwroot/css'));
});


// -----------------------------------------------------------------------------
// Copy vendor files - list-based copy all in one task
// -----------------------------------------------------------------------------
gulp.task('copy-vendor-files', function() {
    const vendorFiles = [
        {
            src: ['./node_modules/jquery/dist/jquery.min.js', './node_modules/jquery/dist/jquery.min.map'],
            dest: './wwwroot/lib/jquery'
        },
        {
            src: './node_modules/jquery-ui-dist/jquery-ui.min.js',
            dest: './wwwroot/lib/jquery-ui'
        },
        {
            src: './node_modules/masonry-layout/dist/masonry.pkgd.min.js',
            dest: './wwwroot/lib/masonry'
        },
        {
            src: './node_modules/@fortawesome/fontawesome-free/css/all.min.css',
            dest: './wwwroot/lib/fontawesome/css'
        },
        {
            src: './node_modules/@fortawesome/fontawesome-free/webfonts/*',
            dest: './wwwroot/lib/fontawesome/webfonts'
        },
        {
            src: './node_modules/bootstrap-icons/font/bootstrap-icons.min.css',
            dest: './wwwroot/lib/bootstrap-icons/font'
        },
        {
            src: './node_modules/bootstrap-icons/font/fonts/*',
            dest: './wwwroot/lib/bootstrap-icons/font/fonts'
        }
    ];

    const tasks = vendorFiles.map(file => {
        return gulp.src(file.src).pipe(gulp.dest(file.dest));
    });

    return Promise.all(tasks);
});

// -----------------------------------------------------------------------------
// Build all assets
// -----------------------------------------------------------------------------
gulp.task('build', gulp.parallel('sass', 'copy-vendor-files'));

// Default task
gulp.task('default', gulp.series('build'));
